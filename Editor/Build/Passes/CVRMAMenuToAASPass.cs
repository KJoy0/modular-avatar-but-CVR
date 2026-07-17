#if UNITY_EDITOR
using System.Collections.Generic;
using ABI.CCK.Scripts;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Converts CVRMAMenuItem components into CVRAdvancedSettingsEntry entries on the
    /// CVRAvatar's avatarSettings list, then removes the MA components.
    /// </summary>
    internal static class CVRMAMenuToAASPass
    {
        internal static void Run(GameObject avatarRoot)
        {
            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            if (avatar == null) return;

            // Ensure avatar has advanced settings enabled
            avatar.avatarUsesAdvancedSettings = true;
            if (avatar.avatarSettings == null)
                avatar.avatarSettings = new CVRAdvancedAvatarSettings();
            if (avatar.avatarSettings.settings == null)
                avatar.avatarSettings.settings = new List<CVRAdvancedSettingsEntry>();

            var installers = avatarRoot.GetComponentsInChildren<CVRMAMenuInstaller>(true);
            if (installers.Length == 0) return;

            foreach (var installer in installers)
            {
                if (installer == null) continue;

                int insertIndex = ResolveInsertIndex(avatar, installer);
                var entries = CollectEntries(installer.gameObject);

                foreach (var entry in entries)
                {
                    // A reactive pass (ShapeChanger/ObjectToggle/...) may already have
                    // created the entry for this parameter — first one wins.
                    if (avatar.avatarSettings.settings.Exists(s => s.machineName == entry.machineName))
                        continue;

                    if (insertIndex >= 0 && insertIndex <= avatar.avatarSettings.settings.Count)
                    {
                        avatar.avatarSettings.settings.Insert(insertIndex, entry);
                        insertIndex++;
                    }
                    else
                    {
                        avatar.avatarSettings.settings.Add(entry);
                    }
                }

                Object.DestroyImmediate(installer);
            }

            // Remove leftover group/target components
            foreach (var group in avatarRoot.GetComponentsInChildren<CVRMAMenuGroup>(true))
                Object.DestroyImmediate(group);
            foreach (var target in avatarRoot.GetComponentsInChildren<CVRMAMenuInstallTarget>(true))
                Object.DestroyImmediate(target);
            foreach (var item in avatarRoot.GetComponentsInChildren<CVRMAMenuItem>(true))
                Object.DestroyImmediate(item);
        }

        private static int ResolveInsertIndex(ABI.CCK.Components.CVRAvatar avatar, CVRMAMenuInstaller installer)
        {
            if (installer.installTarget == null) return -1;

            // Find the install target's slot name in existing entries by name prefix
            var slotName = installer.installTarget.slotName;
            var settings = avatar.avatarSettings.settings;
            for (int i = 0; i < settings.Count; i++)
            {
                if (settings[i].name == $"[{slotName}]")
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Walks the hierarchy under root collecting CVRMAMenuItem components and
        /// converting them to CVRAdvancedSettingsEntry objects.
        /// Recurses into SubMenu items and MenuGroup containers, flattening them.
        /// Int-typed toggles sharing a parameter are grouped into ONE Dropdown entry
        /// (exclusive selection — the VRC MA radio-toggle pattern).
        /// </summary>
        private static List<CVRAdvancedSettingsEntry> CollectEntries(GameObject root)
        {
            var items = new List<CVRMAMenuItem>();
            CollectRecursive(root.transform, items, skipSelf: true);
            return ConvertItems(items);
        }

        private static void CollectRecursive(Transform node, List<CVRMAMenuItem> results, bool skipSelf)
        {
            if (!skipSelf)
            {
                var item = node.GetComponent<CVRMAMenuItem>();
                if (item != null)
                {
                    if (item.controlType == CVRMAControlType.SubMenu)
                    {
                        // Flatten: recurse into children
                        foreach (Transform child in node)
                            CollectRecursive(child, results, skipSelf: false);
                        return;
                    }

                    results.Add(item);
                    return; // Don't recurse further into a non-submenu item
                }

                // If no menu item but has a group, recurse children
                var group = node.GetComponent<CVRMAMenuGroup>();
                if (group != null)
                {
                    var src = group.EffectiveSourceObject;
                    foreach (Transform child in src.transform)
                        CollectRecursive(child, results, skipSelf: false);
                    return;
                }
            }

            // Recurse into all children when skipSelf or no relevant component
            foreach (Transform child in node)
                CollectRecursive(child, results, skipSelf: false);
        }

        private static List<CVRAdvancedSettingsEntry> ConvertItems(List<CVRMAMenuItem> items)
        {
            var results = new List<CVRAdvancedSettingsEntry>();
            // machineName → shared Dropdown entry for Int toggles
            var dropdowns = new Dictionary<string, CVRAdvancedSettingsEntry>();

            foreach (var item in items)
            {
                bool isToggle = item.controlType == CVRMAControlType.Toggle ||
                                item.controlType == CVRMAControlType.Button;

                if (isToggle && item.parameterType == CVRMAMenuParameterType.Int)
                {
                    AddToDropdown(item, dropdowns, results);
                    continue;
                }

                var entry = ConvertItem(item);
                if (entry != null) results.Add(entry);
            }
            return results;
        }

        /// <summary>
        /// Merges an Int toggle into its parameter's Dropdown entry: the option at
        /// index round(value) gets this item's label. Gaps become "Option N" and
        /// index 0 defaults to "Off" so the dropdown indices line up with the values.
        /// </summary>
        private static void AddToDropdown(
            CVRMAMenuItem item,
            Dictionary<string, CVRAdvancedSettingsEntry> dropdowns,
            List<CVRAdvancedSettingsEntry> results)
        {
            var machineName = item.GetEffectiveMachineName();
            int value = Mathf.RoundToInt(item.value);
            if (value < 0)
            {
                Debug.LogWarning(
                    $"[MA-CVR] Menu item '{item.gameObject.name}': Int toggle value {value} is negative — " +
                    "CVR dropdowns only support values ≥ 0. Skipped.");
                return;
            }

            if (!dropdowns.TryGetValue(machineName, out var entry))
            {
                entry = new CVRAdvancedSettingsEntry
                {
                    name        = machineName,
                    machineName = machineName,
                    type        = CVRAdvancedSettingsEntry.SettingsType.Dropdown,
                    dropDownSettings = new CVRAdvancesAvatarSettingGameObjectDropdown
                    {
                        defaultValue = 0,
                        usedType     = CVRAdvancesAvatarSettingBase.ParameterType.Int,
                        options      = new List<CVRAdvancedSettingsDropDownEntry>()
                    }
                };
                dropdowns[machineName] = entry;
                results.Add(entry);
            }

            var options = entry.dropDownSettings.options;
            while (options.Count <= value)
                options.Add(new CVRAdvancedSettingsDropDownEntry
                {
                    name = options.Count == 0 ? "Off" : $"Option {options.Count}"
                });

            options[value].name = item.GetEffectiveLabel();
            if (item.defaultValue > 0.5f)
                entry.dropDownSettings.defaultValue = value;
        }

        private static CVRAdvancedSettingsEntry ConvertItem(CVRMAMenuItem item)
        {
            var entry = new CVRAdvancedSettingsEntry
            {
                name = item.GetEffectiveLabel(),
                machineName = item.GetEffectiveMachineName()
            };

            switch (item.controlType)
            {
                case CVRMAControlType.Toggle:
                case CVRMAControlType.Button:
                    // Int toggles are grouped into Dropdowns before this method is reached.
                    bool isFloat = item.parameterType == CVRMAMenuParameterType.Float;
                    if (isFloat && !Mathf.Approximately(item.value, 1f))
                        Debug.LogWarning(
                            $"[MA-CVR] Menu item '{item.gameObject.name}': CVR float toggles write 0/1, " +
                            $"not {item.value}. Use an Int parameter (dropdown) for multi-value selection.");
                    entry.type = CVRAdvancedSettingsEntry.SettingsType.Toggle;
                    entry.toggleSettings = new CVRAdvancesAvatarSettingGameObjectToggle
                    {
                        defaultValue = item.defaultValue > 0.5f,
                        usedType = isFloat
                            ? CVRAdvancesAvatarSettingBase.ParameterType.Float
                            : CVRAdvancesAvatarSettingBase.ParameterType.Bool
                    };
                    break;

                case CVRMAControlType.RadialPuppet:
                    entry.type = CVRAdvancedSettingsEntry.SettingsType.Slider;
                    entry.sliderSettings = new CVRAdvancesAvatarSettingSlider
                    {
                        defaultValue = item.defaultValue,
                        usedType = CVRAdvancesAvatarSettingBase.ParameterType.Float
                    };
                    // Use the parameter as machine name (CVR Slider uses single machineName)
                    if (!string.IsNullOrEmpty(item.parameter))
                        entry.machineName = item.parameter;
                    break;

                case CVRMAControlType.TwoAxisPuppet:
                    entry.type = CVRAdvancedSettingsEntry.SettingsType.Joystick2D;
                    entry.joystick2DSetting = new CVRAdvancesAvatarSettingJoystick2D
                    {
                        defaultValue = Vector2.zero,
                        usedType = CVRAdvancesAvatarSettingBase.ParameterType.Float
                    };
                    // CVR uses machineName-x and machineName-y; use joystickBaseName if set
                    if (!string.IsNullOrEmpty(item.joystickBaseName))
                        entry.machineName = item.joystickBaseName;
                    break;

                case CVRMAControlType.FourAxisPuppet:
                    // Best approximation: use Joystick3D (x/y/z, 4th axis discarded)
                    entry.type = CVRAdvancedSettingsEntry.SettingsType.Joystick3D;
                    entry.joystick3DSetting = new CVRAdvancesAvatarSettingJoystick3D
                    {
                        defaultValue = Vector3.zero,
                        usedType = CVRAdvancesAvatarSettingBase.ParameterType.Float
                    };
                    if (!string.IsNullOrEmpty(item.joystickBaseName))
                        entry.machineName = item.joystickBaseName;
                    break;

                default:
                    Debug.LogWarning($"[MA-CVR] Unhandled control type '{item.controlType}' on '{item.gameObject.name}' — skipped.");
                    return null;
            }

            return entry;
        }
    }
}
#endif
