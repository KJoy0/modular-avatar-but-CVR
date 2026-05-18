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
                var entry = BuildEntry(avatarRoot, swapper);
                if (entry != null)
                    avatar.avatarSettings.settings.Add(entry);
                Object.DestroyImmediate(swapper);
            }
        }

        private static CVRAdvancedSettingsEntry BuildEntry(GameObject avatarRoot, CVRMAMaterialSwap swapper)
        {
            if (swapper.swaps == null || swapper.swaps.Count == 0) return null;

            var root = swapper.swapRoot != null ? swapper.swapRoot : avatarRoot.transform;
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            var onClip  = new AnimationClip { name = $"{swapper.GetEffectiveParameter()}_MatSwap_On" };
            var offClip = new AnimationClip { name = $"{swapper.GetEffectiveParameter()}_MatSwap_Off" };
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
                return null;
            }

            var onPath  = $"{TempFolder}/MA_CVR_{swapper.GetEffectiveParameter()}_On_{GUID.Generate()}.anim";
            var offPath = $"{TempFolder}/MA_CVR_{swapper.GetEffectiveParameter()}_Off_{GUID.Generate()}.anim";
            AssetDatabase.CreateAsset(onClip,  onPath);
            AssetDatabase.CreateAsset(offClip, offPath);

            return new CVRAdvancedSettingsEntry
            {
                name        = swapper.GetEffectiveLabel(),
                machineName = swapper.GetEffectiveParameter(),
                type        = CVRAdvancedSettingsEntry.SettingsType.Toggle,
                toggleSettings = new CVRAdvancesAvatarSettingGameObjectToggle
                {
                    defaultValue     = swapper.defaultValue,
                    usedType         = CVRAdvancesAvatarSettingBase.ParameterType.Bool,
                    useAnimationClip = true,
                    animationClip    = onClip,
                    offAnimationClip = offClip
                }
            };
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
