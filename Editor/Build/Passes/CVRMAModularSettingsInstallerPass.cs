#if UNITY_EDITOR
using System.Collections.Generic;
using ABI.CCK.Scripts;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Reads all CVRMAModularSettingsInstaller components on the avatar and
    /// appends the AAS entries from their referenced CVRMAModularSettings
    /// assets into the avatar's avatarSettings.settings list.
    /// </summary>
    internal static class CVRMAModularSettingsInstallerPass
    {
        internal static void Run(GameObject avatarRoot)
        {
            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            if (avatar == null) return;

            var installers = avatarRoot.GetComponentsInChildren<CVRMAModularSettingsInstaller>(true);
            if (installers.Length == 0) return;

            avatar.avatarUsesAdvancedSettings = true;
            if (avatar.avatarSettings == null)
                avatar.avatarSettings = new CVRAdvancedAvatarSettings();
            if (avatar.avatarSettings.settings == null)
                avatar.avatarSettings.settings = new List<CVRAdvancedSettingsEntry>();

            foreach (var installer in installers)
            {
                Apply(avatar, installer);
                Object.DestroyImmediate(installer);
            }
        }

        /// <summary>
        /// Applies a single installer's modular settings to the avatar.
        /// Public so the inspector's "Sync to Avatar Now" button can call it.
        /// </summary>
        internal static int Apply(ABI.CCK.Components.CVRAvatar avatar, CVRMAModularSettingsInstaller installer)
        {
            if (installer.modularSettings == null) return 0;

            avatar.avatarUsesAdvancedSettings = true;
            if (avatar.avatarSettings == null)
                avatar.avatarSettings = new CVRAdvancedAvatarSettings();
            if (avatar.avatarSettings.settings == null)
                avatar.avatarSettings.settings = new List<CVRAdvancedSettingsEntry>();

            int added = 0;
            foreach (var bundle in installer.modularSettings)
            {
                if (bundle == null || bundle.settings == null) continue;

                foreach (var entry in bundle.settings)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.machineName)) continue;

                    int existingIndex = avatar.avatarSettings.settings.FindIndex(
                        s => s.machineName == entry.machineName);

                    if (existingIndex >= 0)
                    {
                        if (installer.laterOverridesEarlier)
                            avatar.avatarSettings.settings[existingIndex] = entry;
                        // else: skip, first wins
                    }
                    else
                    {
                        avatar.avatarSettings.settings.Add(entry);
                        added++;
                    }
                }
            }

            return added;
        }
    }
}
#endif
