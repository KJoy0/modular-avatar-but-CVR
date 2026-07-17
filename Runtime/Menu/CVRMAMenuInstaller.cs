using UnityEngine;

namespace ModularAvatarCVR
{
    /// <summary>
    /// Marks a subtree of CVRMAMenuItem components for installation into the avatar's
    /// Advanced Avatar Settings at build time.
    ///
    /// Place this component on the root of an accessory or outfit object.
    /// All CVRMAMenuItem descendants will be collected and added to the avatar's AAS.
    ///
    /// If a CVRMAMenuInstallTarget is referenced, items are inserted at that position
    /// in the settings list; otherwise they are appended to the end.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Menu Installer")]
    public class CVRMAMenuInstaller : CVRMAComponent
    {
        [Tooltip("Where in the AAS list to insert these items. Leave null to append.")]
        public CVRMAMenuInstallTarget installTarget;
    }
}
