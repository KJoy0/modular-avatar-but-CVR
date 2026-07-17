using UnityEngine;

namespace ModularAvatarCVR
{
    /// <summary>
    /// Adjusts this bone's scale at build time WITHOUT distorting its children —
    /// child positions and scales are compensated so only geometry skinned to
    /// this bone changes size. The CVR-MA equivalent of MA Scale Adjuster,
    /// commonly used to fit outfits to differently-proportioned bodies.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Scale Adjuster")]
    [DisallowMultipleComponent]
    public class CVRMAScaleAdjuster : CVRMAComponent
    {
        [Tooltip("Scale multiplier applied to this bone at build time.")]
        public Vector3 scale = Vector3.one;
    }
}
