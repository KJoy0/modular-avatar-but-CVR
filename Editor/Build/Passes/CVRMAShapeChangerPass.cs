#if UNITY_EDITOR
using System.Collections.Generic;
using ABI.CCK.Scripts;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Converts each CVRMAShapeChanger into a Bool-driven animator layer (ON/OFF blendshape
    /// clips) merged into the avatar's controller, plus a slim AAS menu entry that registers
    /// the parameter. See CVRMAAnimatorUtil for why we can't use AAS useAnimationClip.
    /// </summary>
    internal static class CVRMAShapeChangerPass
    {
        internal static void Run(GameObject avatarRoot)
        {
            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            if (avatar == null) return;

            var changers = avatarRoot.GetComponentsInChildren<CVRMAShapeChanger>(true);
            if (changers.Length == 0) return;

            avatar.avatarUsesAdvancedSettings = true;
            if (avatar.avatarSettings == null)
                avatar.avatarSettings = new CVRAdvancedAvatarSettings();
            if (avatar.avatarSettings.settings == null)
                avatar.avatarSettings.settings = new List<CVRAdvancedSettingsEntry>();

            foreach (var changer in changers)
            {
                if (changer.shapes == null || changer.shapes.Count == 0)
                {
                    Object.DestroyImmediate(changer);
                    continue;
                }

                var machineName = changer.GetEffectiveParameter();

                var onClip  = BuildClip(avatarRoot, changer, active: true,  name: $"{machineName}_ShapeOn");
                var offClip = BuildClip(avatarRoot, changer, active: false, name: $"{machineName}_ShapeOff");

                // Build a real animator layer and merge it (the AAS clips path is a dead end).
                var (paramType, compareValue, menuOwned) =
                    CVRMAAnimatorUtil.ResolveMenuBinding(changer.gameObject, machineName);
                var controller = CVRMAAnimatorUtil.BuildToggleController(
                    machineName, onClip, offClip, changer.defaultValue, paramType, compareValue);
                if (controller != null)
                    CVRMAAnimatorUtil.InjectMergeAnimator(changer.gameObject, controller);

                // Register the menu parameter (one entry per machine name) — unless an
                // Int/Float menu item owns it (the MenuToAAS pass creates that entry).
                if (!menuOwned)
                    CVRMAAASUtil.AddOrMergeToggleEntry(avatar,
                        CVRMAAnimatorUtil.BuildMenuEntry(changer.gameObject.name, machineName, changer.defaultValue));

                Object.DestroyImmediate(changer);
            }
        }

        private static AnimationClip BuildClip(
            GameObject avatarRoot, CVRMAShapeChanger changer, bool active, string name)
        {
            var clip = new AnimationClip { name = name };
            bool any = false;

            foreach (var shape in changer.shapes)
            {
                if (shape.targetMesh == null || string.IsNullOrEmpty(shape.shapeName)) continue;

                var path = GetPath(avatarRoot.transform, shape.targetMesh.transform);
                if (path == null) continue;

                // ON: apply the change (Set → value, Delete → 0).
                // OFF: restore the mesh's CURRENT authored weight — not 0, since many
                // meshes ship with non-zero resting weights (body shaping etc.).
                float targetValue;
                if (active)
                {
                    targetValue = shape.changeType == CVRMAShapeChangeType.Delete ? 0f : shape.value;
                }
                else
                {
                    int idx = shape.targetMesh.sharedMesh != null
                        ? shape.targetMesh.sharedMesh.GetBlendShapeIndex(shape.shapeName)
                        : -1;
                    if (idx < 0)
                    {
                        Debug.LogWarning(
                            $"[MA-CVR] ShapeChanger '{changer.gameObject.name}': blendshape " +
                            $"'{shape.shapeName}' not found on '{shape.targetMesh.name}' — skipped.");
                        continue;
                    }
                    targetValue = shape.targetMesh.GetBlendShapeWeight(idx);
                }

                var curve = new AnimationCurve();
                curve.AddKey(new Keyframe(0f,       targetValue) { outTangent = Mathf.Infinity });
                curve.AddKey(new Keyframe(1f / 60f, targetValue) { outTangent = Mathf.Infinity });

                clip.SetCurve(path, typeof(SkinnedMeshRenderer), $"blendShape.{shape.shapeName}", curve);
                any = true;
            }

            if (!any)
                Debug.LogWarning($"[MA-CVR] ShapeChanger '{changer.gameObject.name}': no valid shapes to animate.");

            return clip;
        }

        private static string GetPath(Transform root, Transform target)
        {
            if (target == root) return "";
            var parts = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return current == null ? null : string.Join("/", parts);
        }
    }
}
#endif
