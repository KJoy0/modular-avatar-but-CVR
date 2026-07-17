#if UNITY_EDITOR
using ABI.CCK.Scripts;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Shared builder for the reactive toggle components (ObjectToggle, ShapeChanger,
    /// MaterialSwap, MaterialSetter).
    ///
    /// IMPORTANT: CVR does NOT regenerate the AAS animator at build — that only happens
    /// when the user clicks "Create Controller" in the CVRAvatar inspector. Worse, our
    /// MergeAnimator pass owns the avatar's override controller. So an AAS Toggle entry
    /// with useAnimationClip=true is a dead end: its clips never reach a runtime animator.
    ///
    /// Instead, each reactive component must build its OWN AnimatorController (a Bool
    /// parameter + ON/OFF states playing its clips) and inject a CVRMAMergeAnimator so the
    /// MergeAnimator pass folds it into the override controller. The AAS entry then only
    /// needs to register the menu parameter (the AAS menu drives the same Bool parameter).
    /// </summary>
    internal static class CVRMAAnimatorUtil
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";

        /// <summary>
        /// Builds a toggle controller: OFF state plays <paramref name="offClip"/>, ON state
        /// plays <paramref name="onClip"/>, switched by <paramref name="machineName"/>.
        /// The parameter may be Bool (on when true), Int (on when == compareValue, the
        /// MA radio-toggle pattern), or Float (on when &gt; compareValue − 0.01).
        /// Clips are embedded as sub-assets. Pass freshly-created in-memory clips (NOT existing
        /// assets). Returns null if both clips are null.
        /// </summary>
        internal static AnimatorController BuildToggleController(
            string machineName, AnimationClip onClip, AnimationClip offClip, bool defaultValue,
            AnimatorControllerParameterType paramType = AnimatorControllerParameterType.Bool,
            float compareValue = 1f)
        {
            if (onClip == null && offClip == null) return null;
            EnsureTempFolder();

            var ctrl = new AnimatorController { name = $"MA_CVR_Toggle_{machineName}" };
            ctrl.AddParameter(machineName, paramType);

            var sm = new AnimatorStateMachine { name = $"MA_Toggle_{machineName}" };

            var offState = sm.AddState("OFF");
            offState.motion = offClip;
            offState.writeDefaultValues = false;

            var onState = sm.AddState("ON");
            onState.motion = onClip;
            onState.writeDefaultValues = false;

            sm.defaultState = defaultValue ? onState : offState;

            var toOn = offState.AddTransition(onState);
            toOn.hasExitTime = false;
            toOn.duration    = 0f;

            var toOff = onState.AddTransition(offState);
            toOff.hasExitTime = false;
            toOff.duration    = 0f;

            switch (paramType)
            {
                case AnimatorControllerParameterType.Int:
                    float intValue = Mathf.Round(compareValue);
                    toOn.AddCondition(AnimatorConditionMode.Equals, intValue, machineName);
                    toOff.AddCondition(AnimatorConditionMode.NotEqual, intValue, machineName);
                    break;

                case AnimatorControllerParameterType.Float:
                    float threshold = compareValue - 0.01f;
                    toOn.AddCondition(AnimatorConditionMode.Greater, threshold, machineName);
                    toOff.AddCondition(AnimatorConditionMode.Less, threshold, machineName);
                    break;

                default: // Bool
                    toOn.AddCondition(AnimatorConditionMode.If, 0f, machineName);
                    toOff.AddCondition(AnimatorConditionMode.IfNot, 0f, machineName);
                    break;
            }

            ctrl.AddLayer(new AnimatorControllerLayer
            {
                name          = $"MA_Toggle_{machineName}",
                defaultWeight = 1f,
                stateMachine  = sm
            });

            var path = $"{TempFolder}/MA_CVR_Toggle_{machineName}_{GUID.Generate()}.controller";
            AssetDatabase.CreateAsset(ctrl, path);
            AssetDatabase.AddObjectToAsset(sm, ctrl); sm.hideFlags = HideFlags.HideInHierarchy;
            if (onClip != null)  { AssetDatabase.AddObjectToAsset(onClip, ctrl);  onClip.hideFlags  = HideFlags.HideInHierarchy; }
            if (offClip != null) { AssetDatabase.AddObjectToAsset(offClip, ctrl); offClip.hideFlags = HideFlags.HideInHierarchy; }
            AssetDatabase.SaveAssets();

            return ctrl;
        }

        /// <summary>
        /// Adds a CVRMAMergeAnimator on <paramref name="host"/> referencing <paramref name="ctrl"/>,
        /// so CVRMAMergeAnimatorPass merges it into the avatar's override controller.
        /// Uses Absolute path mode because reactive clips already carry avatar-root-relative paths.
        /// </summary>
        internal static void InjectMergeAnimator(GameObject host, AnimatorController ctrl)
        {
            if (ctrl == null) return;
            var merger = host.AddComponent<CVRMAMergeAnimator>();
            merger.animator               = ctrl;
            merger.pathMode               = CVRMAPathMode.Absolute;
            merger.deleteAttachedAnimator = false;
        }

        /// <summary>
        /// Resolves how a reactive component's parameter is driven. When the parameter is
        /// inherited from a parent MA Menu Item Toggle/Button, that item's parameter type
        /// and value decide the animator conditions — and for Int/Float items the MenuToAAS
        /// pass owns the AAS entry (Int toggles group into a Dropdown), so the reactive
        /// pass must NOT register its own Bool toggle entry.
        /// </summary>
        internal static (AnimatorControllerParameterType paramType, float compareValue, bool menuItemOwned)
            ResolveMenuBinding(GameObject host, string machineName)
        {
            var item = host.GetComponentInParent<CVRMAMenuItem>(true);
            if (item != null && item.GetEffectiveMachineName() == machineName &&
                (item.controlType == CVRMAControlType.Toggle || item.controlType == CVRMAControlType.Button))
            {
                switch (item.parameterType)
                {
                    case CVRMAMenuParameterType.Int:
                        return (AnimatorControllerParameterType.Int, item.value, true);
                    case CVRMAMenuParameterType.Float:
                        return (AnimatorControllerParameterType.Float, item.value, true);
                    default:
                        return (AnimatorControllerParameterType.Bool, 1f, false);
                }
            }
            return (AnimatorControllerParameterType.Bool, 1f, false);
        }

        /// <summary>A slim AAS Toggle entry that only registers the Bool menu parameter.</summary>
        internal static CVRAdvancedSettingsEntry BuildMenuEntry(string name, string machineName, bool defaultValue)
        {
            return new CVRAdvancedSettingsEntry
            {
                name        = name,
                machineName = machineName,
                type        = CVRAdvancedSettingsEntry.SettingsType.Toggle,
                toggleSettings = new CVRAdvancesAvatarSettingGameObjectToggle
                {
                    defaultValue = defaultValue,
                    usedType     = CVRAdvancesAvatarSettingBase.ParameterType.Bool
                }
            };
        }

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "MA_CVR_Temp");
        }
    }
}
#endif
