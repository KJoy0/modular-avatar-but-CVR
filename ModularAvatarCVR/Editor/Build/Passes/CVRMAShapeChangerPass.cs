#if UNITY_EDITOR
using System.Collections.Generic;
using ABI.CCK.Scripts;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Converts each CVRMAShapeChanger into a CVR AAS Toggle entry whose ON/OFF
    /// animation clips set or zero the specified blendshapes.
    /// </summary>
    internal static class CVRMAShapeChangerPass
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";

        internal static void Run(GameObject avatarRoot)
        {
            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            if (avatar == null) return;

            var changers = avatarRoot.GetComponentsInChildren<CVRMAShapeChanger>(true);
            if (changers.Length == 0) return;

            EnsureTempFolder();

            avatar.avatarUsesAdvancedSettings = true;
            if (avatar.avatarSettings == null)
                avatar.avatarSettings = new CVRAdvancedAvatarSettings();
            if (avatar.avatarSettings.settings == null)
                avatar.avatarSettings.settings = new List<CVRAdvancedSettingsEntry>();

            foreach (var changer in changers)
            {
                if (changer.shapes == null || changer.shapes.Count == 0) continue;

                var entry = BuildEntry(avatarRoot, changer);
                if (entry != null)
                    avatar.avatarSettings.settings.Add(entry);

                Object.DestroyImmediate(changer);
            }
        }

        private static CVRAdvancedSettingsEntry BuildEntry(GameObject avatarRoot, CVRMAShapeChanger changer)
        {
            var machineName = changer.GetEffectiveParameter();
            var onClip  = BuildClip(avatarRoot, changer, active: true,  name: $"{machineName}_On");
            var offClip = BuildClip(avatarRoot, changer, active: false, name: $"{machineName}_Off");

            var toggleSettings = new CVRAdvancesAvatarSettingGameObjectToggle
            {
                defaultValue   = changer.defaultValue,
                usedType       = CVRAdvancesAvatarSettingBase.ParameterType.Bool,
                useAnimationClip = true,
                animationClip    = onClip,
                offAnimationClip = offClip
            };

            return new CVRAdvancedSettingsEntry
            {
                name        = changer.gameObject.name,
                machineName = machineName,
                type        = CVRAdvancedSettingsEntry.SettingsType.Toggle,
                toggleSettings = toggleSettings
            };
        }

        private static AnimationClip BuildClip(
            GameObject avatarRoot, CVRMAShapeChanger changer, bool active, string name)
        {
            var clip = new AnimationClip { name = name };

            foreach (var shape in changer.shapes)
            {
                if (shape.targetMesh == null || string.IsNullOrEmpty(shape.shapeName)) continue;

                var path = GetPath(avatarRoot.transform, shape.targetMesh.transform);
                if (path == null) continue;

                float targetValue = active
                    ? (shape.changeType == CVRMAShapeChangeType.Delete ? 0f : shape.value)
                    : (changer.defaultValue ? shape.value : 0f);

                var curve = new AnimationCurve();
                curve.AddKey(new Keyframe(0f,       targetValue) { outTangent = Mathf.Infinity });
                curve.AddKey(new Keyframe(1f / 60f, targetValue) { outTangent = Mathf.Infinity });

                clip.SetCurve(path, typeof(SkinnedMeshRenderer),
                    $"blendShape.{shape.shapeName}", curve);
            }

            var assetPath = $"{TempFolder}/MA_CVR_{name}_{GUID.Generate()}.anim";
            AssetDatabase.CreateAsset(clip, assetPath);
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

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "MA_CVR_Temp");
        }
    }
}
#endif
