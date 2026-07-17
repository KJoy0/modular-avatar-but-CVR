#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Converts VRChat constraints (VRC.SDK3.Dynamics.Constraint.Components.*) into Unity's
    /// built-in constraints (UnityEngine.Animations.*), which ChilloutVR supports natively.
    ///
    /// All source data is read through SerializedObject/SerializedProperty, so this works
    /// without any compile-time reference to the VRChat SDK.
    /// </summary>
    internal static class VRCConstraintToUnity
    {
        /// <summary>Returns true if the component was a VRC constraint and was converted.</summary>
        internal static bool TryConvert(Component src)
        {
            switch (src.GetType().Name)
            {
                case "VRCParentConstraint":   ConvertParent(src);   return true;
                case "VRCPositionConstraint": ConvertPosition(src); return true;
                case "VRCRotationConstraint": ConvertRotation(src); return true;
                case "VRCScaleConstraint":    ConvertScale(src);    return true;
                case "VRCAimConstraint":      ConvertAim(src);      return true;
                case "VRCLookAtConstraint":   ConvertLookAt(src);   return true;
                default: return false;
            }
        }

        internal static bool IsVRCConstraint(string typeName)
        {
            switch (typeName)
            {
                case "VRCParentConstraint":
                case "VRCPositionConstraint":
                case "VRCRotationConstraint":
                case "VRCScaleConstraint":
                case "VRCAimConstraint":
                case "VRCLookAtConstraint":
                    return true;
                default: return false;
            }
        }

        // ------------------------------------------------------------------ per-type

        private static void ConvertPosition(Component src)
        {
            var so = new SerializedObject(src);
            var go = src.gameObject;
            Undo.RecordObject(go, "Convert VRC Position Constraint");
            var c = Undo.AddComponent<PositionConstraint>(go);

            c.translationAtRest = V(so, "PositionAtRest");
            c.translationOffset = V(so, "PositionOffset");
            c.translationAxis   = Axes(so, "AffectsPositionX", "AffectsPositionY", "AffectsPositionZ");

            ApplyCommon(c, so, ReadSources(so, go));
            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertRotation(Component src)
        {
            var so = new SerializedObject(src);
            var go = src.gameObject;
            Undo.RecordObject(go, "Convert VRC Rotation Constraint");
            var c = Undo.AddComponent<RotationConstraint>(go);

            c.rotationAtRest = V(so, "RotationAtRest");
            c.rotationOffset = V(so, "RotationOffset");
            c.rotationAxis   = Axes(so, "AffectsRotationX", "AffectsRotationY", "AffectsRotationZ");

            ApplyCommon(c, so, ReadSources(so, go));
            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertScale(Component src)
        {
            var so = new SerializedObject(src);
            var go = src.gameObject;
            Undo.RecordObject(go, "Convert VRC Scale Constraint");
            var c = Undo.AddComponent<ScaleConstraint>(go);

            c.scaleAtRest = VorOne(so, "ScaleAtRest");
            c.scaleOffset = VorOne(so, "ScaleOffset");
            c.scalingAxis = Axes(so, "AffectsScaleX", "AffectsScaleY", "AffectsScaleZ");

            ApplyCommon(c, so, ReadSources(so, go));
            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertParent(Component src)
        {
            var so = new SerializedObject(src);
            var go = src.gameObject;
            Undo.RecordObject(go, "Convert VRC Parent Constraint");
            var c = Undo.AddComponent<ParentConstraint>(go);

            c.translationAtRest = V(so, "PositionAtRest");
            c.rotationAtRest    = V(so, "RotationAtRest");
            c.translationAxis   = Axes(so, "AffectsPositionX", "AffectsPositionY", "AffectsPositionZ");
            c.rotationAxis      = Axes(so, "AffectsRotationX", "AffectsRotationY", "AffectsRotationZ");

            var sources = ReadSources(so, go);
            c.SetSources(sources);

            // Parent constraints store per-source position/rotation offsets.
            var sp = so.FindProperty("Sources");
            if (sp != null && sp.isArray)
            {
                for (int i = 0; i < sp.arraySize && i < c.sourceCount; i++)
                {
                    var e = sp.GetArrayElementAtIndex(i);
                    c.SetTranslationOffset(i, e.FindPropertyRelative("ParentPositionOffset")?.vector3Value ?? Vector3.zero);
                    c.SetRotationOffset(i,    e.FindPropertyRelative("ParentRotationOffset")?.vector3Value ?? Vector3.zero);
                }
            }

            c.weight          = F(so, "GlobalWeight", 1f);
            c.locked          = B(so, "Locked");
            c.constraintActive = B(so, "IsActive");
            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertAim(Component src)
        {
            var so = new SerializedObject(src);
            var go = src.gameObject;
            Undo.RecordObject(go, "Convert VRC Aim Constraint");
            var c = Undo.AddComponent<AimConstraint>(go);

            c.rotationAtRest = V(so, "RotationAtRest");
            c.rotationOffset = V(so, "RotationOffset");
            c.rotationAxis   = AxesOrAll(so, "AffectsRotationX", "AffectsRotationY", "AffectsRotationZ");
            c.aimVector      = VorDefault(so, "AimAxis", Vector3.forward);
            c.upVector       = VorDefault(so, "UpAxis", Vector3.up);
            c.worldUpVector  = VorDefault(so, "WorldUpVector", Vector3.up);
            c.worldUpObject  = T(so, "WorldUpTransform");

            var wup = so.FindProperty("WorldUp");
            if (wup != null)
                c.worldUpType = (AimConstraint.WorldUpType)wup.enumValueIndex; // VRC mirrors Unity's enum order

            ApplyCommon(c, so, ReadSources(so, go));
            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertLookAt(Component src)
        {
            var so = new SerializedObject(src);
            var go = src.gameObject;
            Undo.RecordObject(go, "Convert VRC LookAt Constraint");
            var c = Undo.AddComponent<LookAtConstraint>(go);

            c.rotationAtRest = V(so, "RotationAtRest");
            c.rotationOffset = V(so, "RotationOffset");
            c.roll           = F(so, "Roll");
            c.useUpObject    = B(so, "UseUpTransform");
            c.worldUpObject  = T(so, "WorldUpTransform");

            ApplyCommon(c, so, ReadSources(so, go));
            Undo.DestroyObjectImmediate(src);
        }

        // ------------------------------------------------------------------ shared

        private static void ApplyCommon(IConstraint c, SerializedObject so, List<ConstraintSource> sources)
        {
            c.SetSources(sources);
            c.weight           = F(so, "GlobalWeight", 1f);
            c.locked           = B(so, "Locked");
            c.constraintActive = B(so, "IsActive"); // activate last
        }

        private static List<ConstraintSource> ReadSources(SerializedObject so, GameObject owner)
        {
            var list = new List<ConstraintSource>();
            var sp = so.FindProperty("Sources");
            if (sp == null || !sp.isArray) return list;

            bool sawTransformProp = false;
            for (int i = 0; i < sp.arraySize; i++)
            {
                var e = sp.GetArrayElementAtIndex(i);
                var tp = e.FindPropertyRelative("SourceTransform");
                if (tp != null) sawTransformProp = true;
                list.Add(new ConstraintSource
                {
                    sourceTransform = tp?.objectReferenceValue as Transform,
                    weight          = e.FindPropertyRelative("Weight")?.floatValue ?? 1f
                });
            }

            if (!sawTransformProp && sp.arraySize > 0)
                Debug.LogWarning(
                    $"[MA-CVR] Constraint on '{owner.name}': could not read source transforms " +
                    "(VRC field layout mismatch). Re-assign sources manually.");

            return list;
        }

        private static Axis Axes(SerializedObject so, string x, string y, string z)
        {
            var a = Axis.None;
            if (B(so, x, true)) a |= Axis.X;
            if (B(so, y, true)) a |= Axis.Y;
            if (B(so, z, true)) a |= Axis.Z;
            return a;
        }

        // Aim/LookAt have no per-axis affects in VRC; default to all axes when absent.
        private static Axis AxesOrAll(SerializedObject so, string x, string y, string z)
        {
            if (so.FindProperty(x) == null) return Axis.X | Axis.Y | Axis.Z;
            return Axes(so, x, y, z);
        }

        private static float     F(SerializedObject so, string n, float def = 0f)   => so.FindProperty(n)?.floatValue ?? def;
        private static bool      B(SerializedObject so, string n, bool def = false)  => so.FindProperty(n)?.boolValue ?? def;
        private static Vector3   V(SerializedObject so, string n)                    => so.FindProperty(n)?.vector3Value ?? Vector3.zero;
        private static Transform T(SerializedObject so, string n)                    => so.FindProperty(n)?.objectReferenceValue as Transform;

        private static Vector3 VorDefault(SerializedObject so, string n, Vector3 def)
        {
            var p = so.FindProperty(n);
            if (p == null) return def;
            var v = p.vector3Value;
            return v == Vector3.zero ? def : v;
        }

        private static Vector3 VorOne(SerializedObject so, string n)
        {
            var p = so.FindProperty(n);
            return p == null ? Vector3.one : p.vector3Value;
        }
    }
}
#endif
