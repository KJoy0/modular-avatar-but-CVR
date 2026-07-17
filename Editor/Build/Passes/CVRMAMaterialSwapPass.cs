#if UNITY_EDITOR
using System.Collections.Generic;
using ABI.CCK.Scripts;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    internal static class CVRMAMaterialSwapPass
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";

        internal static void Run(GameObject avatarRoot)
        {
            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            if (avatar == null) return;

            var swappers = avatarRoot.GetComponentsInChildren<CVRMAMaterialSwap>(true);
            if (swappers.Length == 0) return;

            EnsureTempFolder();

            avatar.avatarUsesAdvancedSettings = true;
            if (avatar.avatarSettings == null)
                avatar.avatarSettings = new CVRAdvancedAvatarSettings();
            if (avatar.avatarSettings.settings == null)
                avatar.avatarSettings.settings = new List<CVRAdvancedSettingsEntry>();

            foreach (var swapper in swappers)
            {
                Build(avatar, avatarRoot, swapper);
                Object.DestroyImmediate(swapper);
            }
        }

        private static void Build(ABI.CCK.Components.CVRAvatar avatar, GameObject avatarRoot, CVRMAMaterialSwap swapper)
        {
            if (swapper.swaps == null || swapper.swaps.Count == 0) return;

            var machineName = swapper.GetEffectiveParameter();
            var root = swapper.swapRoot != null ? swapper.swapRoot : avatarRoot.transform;
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            var onClip  = new AnimationClip { name = $"{machineName}_MatSwap_On" };
            var offClip = new AnimationClip { name = $"{machineName}_MatSwap_Off" };
            bool anyKeyframes = false;

            foreach (var renderer in renderers)
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    foreach (var swap in swapper.swaps)
                    {
                        if (mats[i] != swap.from || swap.to == null) continue;

                        var path = GetPath(avatarRoot.transform, renderer.transform);
                        if (path == null) continue;

                        var rendererType = renderer.GetType();
                        var propName = $"m_Materials.Array.data[{i}]";

                        SetMatKey(onClip,  path, rendererType, propName, swap.to);
                        SetMatKey(offClip, path, rendererType, propName, swap.from);
                        anyKeyframes = true;
                    }
                }
            }

            if (!anyKeyframes)
            {
                Debug.LogWarning($"[MA-CVR] MaterialSwap '{swapper.gameObject.name}': no matching materials found under swap root.");
                return;
            }

            // Build a real animator layer and merge it (the AAS clips path is a dead end).
            var (paramType, compareValue, menuOwned) =
                CVRMAAnimatorUtil.ResolveMenuBinding(swapper.gameObject, machineName);
            var controller = CVRMAAnimatorUtil.BuildToggleController(
                machineName, onClip, offClip, swapper.defaultValue, paramType, compareValue);
            if (controller != null)
                CVRMAAnimatorUtil.InjectMergeAnimator(swapper.gameObject, controller);

            // Int/Float menu items own their AAS entry (the MenuToAAS pass creates it).
            if (!menuOwned)
                CVRMAAASUtil.AddOrMergeToggleEntry(avatar,
                    CVRMAAnimatorUtil.BuildMenuEntry(swapper.GetEffectiveLabel(), machineName, swapper.defaultValue));
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
