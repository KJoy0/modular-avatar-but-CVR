using UnityEngine;

namespace ModularAvatarCVR
{
    /// <summary>
    /// Marks this object as visible in first-person view even when it is attached
    /// to the head bone (which CVR's FPR system would otherwise shrink or cull).
    ///
    /// At build time the pass adds an FPRExclusion component with isShown = true
    /// so the CVR TransformHider keeps this object visible in first-person.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Visible Head Accessory")]
    public class CVRMAVisibleHeadAccessory : CVRMAComponent
    {
        [Tooltip("Apply the FPR exclusion to all child transforms as well.")]
        public bool includeChildren = true;

        [Tooltip(
            "Shrink to zero instead of cutting. " +
            "Shrink keeps the mesh for physics; Cut disables the renderer entirely.")]
        public bool shrinkToZero = true;
    }
}
