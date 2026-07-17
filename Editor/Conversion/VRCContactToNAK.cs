#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Converts VRChat Contacts (VRCContactSender / VRCContactReceiver) into NAK Contacts
    /// (NAK.Contacts.ContactSender / ContactReceiver, with a ContactAnimator for parameter
    /// driving on receivers).
    ///
    /// NAK Contacts is an OPTIONAL dependency: all access goes through reflection so this
    /// converter compiles and runs even when the NAK.Contacts package is not installed.
    /// When it's missing, contacts are left untouched and the user is warned.
    /// </summary>
    internal static class VRCContactToNAK
    {
        // ---- NAK type discovery (cached) ----
        private static Type _sender, _receiver, _animator;
        private static bool _resolved;

        private static void ResolveTypes()
        {
            if (_resolved) return;
            _resolved = true;
            _sender   = FindType("NAK.Contacts.ContactSender");
            _receiver = FindType("NAK.Contacts.ContactReceiver");
            _animator = FindType("NAK.Contacts.ContactAnimator");
        }

        internal static bool NAKAvailable
        {
            get { ResolveTypes(); return _sender != null && _receiver != null; }
        }

        internal static bool IsVRCContact(string typeName) =>
            typeName == "VRCContactSender" || typeName == "VRCContactReceiver";

        // ------------------------------------------------------------------ entry

        /// <summary>Returns true if the component was a VRC contact and was converted.</summary>
        internal static bool TryConvert(Component src, GameObject avatarRoot)
        {
            switch (src.GetType().Name)
            {
                case "VRCContactSender":   return ConvertSender(src);
                case "VRCContactReceiver": return ConvertReceiver(src, avatarRoot);
                default: return false;
            }
        }

        // ------------------------------------------------------------------ sender

        private static bool ConvertSender(Component src)
        {
            ResolveTypes();
            if (_sender == null) { WarnMissing(); return false; }

            var so = new SerializedObject(src);
            var go = src.gameObject;
            Undo.RecordObject(go, "Convert VRC Contact Sender");

            var dst = Undo.AddComponent(go, _sender);
            var dso = new SerializedObject(dst);
            CopyShape(so, dso);
            SetEnum(dso, "senderContentType", NAK_CONTENT_AVATAR);
            dso.ApplyModifiedProperties();

            WarnRootTransform(so, go);
            Undo.DestroyObjectImmediate(src);
            return true;
        }

        // ------------------------------------------------------------------ receiver

        private static bool ConvertReceiver(Component src, GameObject avatarRoot)
        {
            ResolveTypes();
            if (_receiver == null) { WarnMissing(); return false; }

            var so = new SerializedObject(src);
            var go = src.gameObject;
            Undo.RecordObject(go, "Convert VRC Contact Receiver");

            var dst = Undo.AddComponent(go, _receiver);
            var dso = new SerializedObject(dst);
            CopyShape(so, dso);
            SetBool(dso, "allowSelf",   so.FindProperty("allowSelf")?.boolValue ?? false);
            SetBool(dso, "allowOthers", so.FindProperty("allowOthers")?.boolValue ?? true);

            // VRC ReceiverType: Constant=0, OnEnter=1, Proximity=2
            // NAK ReceiverType: Constant=0, OnEnter=1, ProximityCenterToCenter=4
            int vrcRt = so.FindProperty("receiverType")?.enumValueIndex ?? 0;
            int nakRt = vrcRt switch
            {
                0 => 0, // Constant
                1 => 1, // OnEnter
                2 => 4, // Proximity → ProximityCenterToCenter (closest VRC analogue)
                _ => 0
            };
            SetEnum(dso, "receiverType", nakRt);
            dso.ApplyModifiedProperties();

            // Wire the parameter through a ContactAnimator (same GameObject).
            var param = so.FindProperty("parameter")?.stringValue ?? "";
            if (!string.IsNullOrEmpty(param))
            {
                if (_animator != null)
                {
                    var anim = Undo.AddComponent(go, _animator);
                    var aso = new SerializedObject(anim);
                    var pp = aso.FindProperty("parameter");
                    if (pp != null) pp.stringValue = param;

                    var animator = FindAnimator(avatarRoot);
                    var ap = aso.FindProperty("animator");
                    if (ap != null && animator != null) ap.objectReferenceValue = animator;
                    aso.ApplyModifiedProperties();

                    if (animator == null)
                        Debug.LogWarning(
                            $"[MA-CVR] Contact Receiver on '{go.name}': no Animator found on the avatar root. " +
                            $"Set ContactAnimator.animator manually so parameter '{param}' is driven.");
                }
                else
                {
                    Debug.LogWarning(
                        $"[MA-CVR] Contact Receiver on '{go.name}': NAK ContactAnimator not found; " +
                        $"parameter '{param}' was not wired up.");
                }
            }

            WarnRootTransform(so, go);
            if (so.FindProperty("localOnly")?.boolValue ?? false)
                Debug.Log(
                    $"[MA-CVR] Contact Receiver on '{go.name}': VRC 'Local Only' has no direct NAK flag; " +
                    "verify networking behaviour after conversion.");

            Undo.DestroyObjectImmediate(src);
            return true;
        }

        // ------------------------------------------------------------------ shared

        // NAK ContentType flags: World=1, Avatar=2, Prop=4, Player=8
        private const int NAK_CONTENT_AVATAR = 2;

        private static void CopyShape(SerializedObject vrc, SerializedObject nak)
        {
            SetVec (nak, "localPosition", vrc.FindProperty("position")?.vector3Value ?? Vector3.zero);

            var rotProp = vrc.FindProperty("rotation");
            SetQuat(nak, "localRotation", rotProp != null ? rotProp.quaternionValue : Quaternion.identity);

            SetFloat(nak, "radius", vrc.FindProperty("radius")?.floatValue ?? 0.5f);
            SetFloat(nak, "height", vrc.FindProperty("height")?.floatValue ?? 1f);

            // Both use Sphere=0, Capsule=1.
            SetEnum(nak, "shapeType", vrc.FindProperty("shapeType")?.enumValueIndex ?? 0);

            CopyTags(vrc, nak);
        }

        private static void CopyTags(SerializedObject vrc, SerializedObject nak)
        {
            var vt = vrc.FindProperty("collisionTags");
            var nt = nak.FindProperty("collisionTags");
            if (vt == null || nt == null || !vt.isArray || !nt.isArray) return;

            int count = Mathf.Min(vt.arraySize, 16); // NAK ContactLimits.MaxTags
            nt.arraySize = count;
            for (int i = 0; i < count; i++)
                nt.GetArrayElementAtIndex(i).stringValue = vt.GetArrayElementAtIndex(i).stringValue;
        }

        private static Animator FindAnimator(GameObject avatarRoot)
        {
            if (avatarRoot == null) return null;
            return avatarRoot.GetComponent<Animator>() ?? avatarRoot.GetComponentInChildren<Animator>(true);
        }

        private static void WarnRootTransform(SerializedObject vrc, GameObject go)
        {
            var rt = vrc.FindProperty("rootTransform")?.objectReferenceValue as Transform;
            if (rt != null && rt != go.transform)
                Debug.LogWarning(
                    $"[MA-CVR] Contact on '{go.name}' used a Root Transform override ('{rt.name}'). " +
                    "NAK contacts are positioned by their own GameObject — move the contact onto " +
                    $"'{rt.name}', or bake the offset into Local Position/Rotation.");
        }

        private static bool _warnedMissing;
        private static void WarnMissing()
        {
            if (_warnedMissing) return;
            _warnedMissing = true;
            Debug.LogWarning(
                "[MA-CVR] VRChat Contacts were found, but the NAK.Contacts package is not installed. " +
                "Install NAK Contacts to convert Contact Senders/Receivers, or remove them manually.");
        }

        // ---- SerializedObject setters (null-safe) ----
        private static void SetFloat(SerializedObject so, string n, float v)      { var p = so.FindProperty(n); if (p != null) p.floatValue = v; }
        private static void SetBool (SerializedObject so, string n, bool v)       { var p = so.FindProperty(n); if (p != null) p.boolValue = v; }
        private static void SetVec  (SerializedObject so, string n, Vector3 v)    { var p = so.FindProperty(n); if (p != null) p.vector3Value = v; }
        private static void SetQuat (SerializedObject so, string n, Quaternion v) { var p = so.FindProperty(n); if (p != null) p.quaternionValue = v; }
        private static void SetEnum (SerializedObject so, string n, int idx)      { var p = so.FindProperty(n); if (p != null) p.enumValueIndex = idx; }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    }
}
#endif
