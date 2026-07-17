#if UNITY_EDITOR
using System.Collections.Generic;
using ABI.CCK.Scripts;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    internal static class CVRMAObjectTogglePass
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";

        internal static void Run(GameObject avatarRoot)
        {
            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            if (avatar == null) return;

            var toggles = avatarRoot.GetComponentsInChildren<CVRMAObjectToggle>(true);
            if (toggles.Length == 0) return;

            EnsureTempFolder();

            avatar.avatarUsesAdvancedSettings = true;
            if (avatar.avatarSettings == null)
                avatar.avatarSettings = new CVRAdvancedAvatarSettings();
            if (avatar.avatarSettings.settings == null)
                avatar.avatarSettings.settings = new List<CVRAdvancedSettingsEntry>();

            foreach (var toggle in toggles)
            {
                var machineName = toggle.GetEffectiveParameter();

                // Always inject an animator layer — the inspector button does not create one.
                var controller = BuildToggleController(avatarRoot, toggle);
                CVRMAAnimatorUtil.InjectMergeAnimator(toggle.gameObject, controller);

                // Int/Float menu items own their AAS entry (the MenuToAAS pass creates it).
                var (_, _, menuOwned) = CVRMAAnimatorUtil.ResolveMenuBinding(toggle.gameObject, machineName);

                // Only add AAS entry if not already present (button may have written it).
                bool alreadyExists = menuOwned;
                foreach (var s in avatar.avatarSettings.settings)
                    if (s.machineName == machineName) { alreadyExists = true; break; }

                if (!alreadyExists)
                {
                    var entry = BuildEntry(toggle);
                    if (entry != null)
                        avatar.avatarSettings.settings.Add(entry);
                }

                Object.DestroyImmediate(toggle);
            }
        }

        /// <summary>
        /// Builds the AAS entry (menu display + parameter registration).
        /// Called by the build pass and the inspector "Apply to AAS Now" button.
        /// The animator layer that drives the animation is created separately by Run().
        /// </summary>
        internal static CVRAdvancedSettingsEntry BuildEntry(CVRMAObjectToggle toggle)
        {
            return new CVRAdvancedSettingsEntry
            {
                name        = toggle.GetEffectiveLabel(),
                machineName = toggle.GetEffectiveParameter(),
                type        = CVRAdvancedSettingsEntry.SettingsType.Toggle,
                toggleSettings = new CVRAdvancesAvatarSettingGameObjectToggle
                {
                    defaultValue = toggle.defaultValue,
                    usedType     = CVRAdvancesAvatarSettingBase.ParameterType.Bool
                }
            };
        }

        private static AnimatorController BuildToggleController(GameObject avatarRoot, CVRMAObjectToggle toggle)
        {
            var machineName = toggle.GetEffectiveParameter();

            var onClip  = new AnimationClip { name = $"{machineName}_ObjToggle_On" };
            var offClip = new AnimationClip { name = $"{machineName}_ObjToggle_Off" };
            bool anyKeyframes = false;

            foreach (var obj in toggle.objects)
            {
                if (obj.target == null) continue;
                var path = GetPath(avatarRoot.transform, obj.target);
                if (path == null) continue;

                SetActiveKey(onClip,  path, obj.activeWhenOn);
                SetActiveKey(offClip, path, !obj.activeWhenOn);
                anyKeyframes = true;
            }

            if (!anyKeyframes)
            {
                Debug.LogWarning($"[MA-CVR] ObjectToggle '{toggle.gameObject.name}': no valid targets found.");
                return null;
            }

            var (paramType, compareValue, _) =
                CVRMAAnimatorUtil.ResolveMenuBinding(toggle.gameObject, machineName);
            return CVRMAAnimatorUtil.BuildToggleController(
                machineName, onClip, offClip, toggle.defaultValue, paramType, compareValue);
        }

        private static void SetActiveKey(AnimationClip clip, string path, bool active)
        {
            var binding = new EditorCurveBinding
            {
                path         = path,
                type         = typeof(GameObject),
                propertyName = "m_IsActive"
            };
            var curve = new AnimationCurve(
                new Keyframe(0f,       active ? 1f : 0f),
                new Keyframe(1f / 60f, active ? 1f : 0f)
            );
            AnimationUtility.SetEditorCurve(clip, binding, curve);
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
