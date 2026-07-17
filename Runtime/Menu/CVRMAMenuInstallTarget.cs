using UnityEngine;

namespace ModularAvatarCVR
{
    /// <summary>
    /// Marks a position in the avatar's AAS settings list where CVRMAMenuInstaller
    /// components can inject their items.
    ///
    /// Place this on the avatar root or on a dedicated child object to define
    /// install slots for accessories.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Menu Install Target")]
    public class CVRMAMenuInstallTarget : CVRMAComponent
    {
        [Tooltip("Name of this install slot, used by CVRMAMenuInstaller to target it.")]
        public string slotName = "Default";
    }
}
