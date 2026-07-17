using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR
{
    /// <summary>
    /// Installs one or more CVRMAModularSettings assets onto the avatar.
    ///
    /// At build time the referenced ScriptableObjects' AAS entries are appended
    /// to the avatar's avatarSettings.settings list. Duplicate machine names
    /// already present on the avatar are skipped.
    ///
    /// Can also be applied at edit time via the "Sync to Avatar Now" button.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Modular Settings Installer")]
    public class CVRMAModularSettingsInstaller : CVRMAComponent
    {
        [Tooltip("Modular settings assets to install onto the avatar.")]
        public List<CVRMAModularSettings> modularSettings = new List<CVRMAModularSettings>();

        [Tooltip(
            "If true, entries from later assets in the list with the same machine name " +
            "as an earlier one will override the earlier one. " +
            "If false (default), the first wins.")]
        public bool laterOverridesEarlier = false;
    }
}
