using System.Collections.Generic;
using ABI.CCK.Scripts;
using UnityEngine;

namespace ModularAvatarCVR
{
    /// <summary>
    /// Reusable container of CVR Advanced Avatar Settings entries that can be
    /// installed onto any avatar via CVRMAModularSettingsInstaller.
    ///
    /// Create via: Assets → Create → Modular Avatar CVR → Modular Settings
    /// </summary>
    [CreateAssetMenu(
        fileName = "CVRMA_Modular_Settings",
        menuName = "Modular Avatar CVR/Modular Settings")]
    public class CVRMAModularSettings : ScriptableObject
    {
        [Tooltip("AAS entries to install onto the avatar at build time.")]
        public List<CVRAdvancedSettingsEntry> settings = new List<CVRAdvancedSettingsEntry>();
    }
}
