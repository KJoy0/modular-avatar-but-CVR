#if UNITY_EDITOR
using System.Collections.Generic;
using ABI.CCK.Scripts;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    internal static class CVRMAMaterialSetterPass
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";

        internal static void Run(GameObject avatarRoot)
        {
            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            if (avatar == null) return;

            var setters = avatarRoot.GetComponentsInChildren<CVRMAMaterialSetter>(true);
            if (setters.Length == 0) return;

            EnsureTempFolder();

            avatar.avatarUsesAdvancedSettings = true;
            if (avatar.avatarSettings == null)
                avatar.avatarSettings = new CVRAdvancedAvatarSettings();
            if (avatar.avatarSettings.settings == null)
                avatar.avatarSettings.settings = new List<CVRAdvancedSettingsEntry>();

            foreach (var setter in setters)
            {
                Build(avatar, avatarRoot, setter);
                Object.DestroyImmediate(setter);
            }
        }

        private static void Build(ABI.CCK.Components.CVRAvatar avatar, GameObject avatarRoot, CVRMAMaterialSetter setter)
        {
            if (setter.entries == null || setter.entries.Count == 0) return;

            var machineName = setter.GetEffectiveParameter();
            var onClip  = new AnimationClip { name = $"{machineName}_MatSet_On" };
            var offClip = new AnimationClip { name = $"{machineName}_MatSet_Off" };
            bool anyKeyframes = false;

            foreach (var entry in setter.entries)
            {
                if (entry.targetRenderer == null || entry.material == null) continue;

                var path = GetPath(avatarRoot.transform, entry.targetRenderer.transform);
                if (path == null) continue;

                var rendererType = entry.targetRenderer.GetType();
                var propName = $"m_Materials.Array.data[{entry.materialIndex}]";

                // ON: apply the new material
                SetMatKey(onClip, path, rendererType, propName, entry.material);

                // OFF: revert to the original material
                var origMats = entry.targetRenderer.sharedMaterials;
                var origMat = entry.materialIndex < origMats.Length ? origMats[entry.materialIndex] : null;
                SetMatKey(offClip, path, rendererType, propName, origMat);

                anyKeyframes = true;
            }

            if (!anyKeyframes)
            {
                Debug.LogWarning($"[MA-CVR] MaterialSetter '{setter.gameObject.name}': no valid entries found.");
                return;
            }

            // Build a real animator layer and merge it (the AAS clips path is a dead end).
            var (paramType, compareValue, menuOwned) =
                CVRMAAnimatorUtil.ResolveMenuBinding(setter.gameObject, machineName);
            var controller = CVRMAAnimatorUtil.BuildToggleController(
                machineName, onClip, offClip, setter.defaultValue, paramType, compareValue);
            if (controller != null)
                CVRMAAnimatorUtil.InjectMergeAnimator(setter.gameObject, controller);

            // Int/Float menu items own their AAS entry (the MenuToAAS pass creates it).
            if (!menuOwned)
                CVRMAAASUtil.AddOrMergeToggleEntry(avatar,
                    CVRMAAnimatorUtil.BuildMenuEntry(setter.GetEffectiveLabel(), machineName, setter.defaultValue));
        }

        private static void SetMatKey(AnimationClip clip, string path, System.Type rendererType,
            string propName, Material mat)
        {
            var binding = new EditorCurveBinding
            {
                path         = path,
                type         = rendererType,
                propertyName = propName
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, new[]
            {
                new ObjectReferenceKeyframe { time = 0f,       value = mat },
                new ObjectReferenceKeyframe { time = 1f / 60f, value = mat }
            });
        }

        private static string GetPath(Transform root, Transform target)
        {
            if (target == root) return "";
            var parts = new List<string>();
            var cur = target;
            while (cur != null && cur != root) { parts.Insert(0, cur.name); cur = cur.parent; }
            return cur == null ? null : string.Join("/", parts);
        }

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "MA_CVR_Temp");
        }
    }
}
#endif
