#if UNITY_EDITOR
using System.Collections.Generic;
using ABI.CCK.Scripts;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Registers CVRMAParameters entries as AAS settings so the parameters are
    /// network-synced. Skips entries that already exist in the AAS list by machineName.
    /// </summary>
    internal static class CVRMAParametersPass
    {
        internal static void Run(GameObject avatarRoot)
        {
            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            if (avatar == null) return;

            var paramComponents = avatarRoot.GetComponentsInChildren<CVRMAParameters>(true);
            if (paramComponents.Length == 0) return;

            avatar.avatarUsesAdvancedSettings = true;
            if (avatar.avatarSettings == null)
                avatar.avatarSettings = new CVRAdvancedAvatarSettings();
            if (avatar.avatarSettings.settings == null)
                avatar.avatarSettings.settings = new List<CVRAdvancedSettingsEntry>();

            // Build a set of already-declared machine names so we don't duplicate
            var declared = new HashSet<string>();
            foreach (var existing in avatar.avatarSettings.settings)
                declared.Add(existing.machineName);

            foreach (var comp in paramComponents)
            {
                foreach (var param in comp.parameters)
                {
                    if (param.syncType == CVRMAParameterSyncType.NotSynced) continue;
                    if (string.IsNullOrEmpty(param.nameOrPrefix)) continue;

                    var machineName = string.IsNullOrEmpty(param.remapTo)
                        ? param.nameOrPrefix
                        : param.remapTo;

                    if (!declared.Add(machineName)) continue; // already declared

                    var entry = BuildEntry(param, machineName);
                    if (entry != null)
                        avatar.avatarSettings.settings.Add(entry);
                }

                Object.DestroyImmediate(comp);
            }
        }

        private static CVRAdvancedSettingsEntry BuildEntry(CVRMAParameterConfig param, string machineName)
        {
            var entry = new CVRAdvancedSettingsEntry
            {
                name        = machineName,
                machineName = machineName
            };

            switch (param.syncType)
            {
                case CVRMAParameterSyncType.Bool:
                    entry.type = CVRAdvancedSettingsEntry.SettingsType.Toggle;
                    entry.toggleSettings = new CVRAdvancesAvatarSettingGameObjectToggle
                    {
                        defaultValue = param.defaultValue > 0.5f,
                        usedType     = CVRAdvancesAvatarSettingBase.ParameterType.Bool
                    };
                    break;

                case CVRMAParameterSyncType.Float:
                    entry.type = CVRAdvancedSettingsEntry.SettingsType.Slider;
                    entry.sliderSettings = new CVRAdvancesAvatarSettingSlider
                    {
                        defaultValue = param.defaultValue,
                        usedType     = CVRAdvancesAvatarSettingBase.ParameterType.Float
                    };
                    break;

                case CVRMAParameterSyncType.Int:
                    entry.type = CVRAdvancedSettingsEntry.SettingsType.Dropdown;
                    entry.dropDownSettings = new CVRAdvancesAvatarSettingGameObjectDropdown
                    {
                        defaultValue = Mathf.RoundToInt(param.defaultValue),
                        usedType     = CVRAdvancesAvatarSettingBase.ParameterType.Int
                    };
                    break;

                default:
                    return null;
            }

            return entry;
        }
    }
}
#endif
