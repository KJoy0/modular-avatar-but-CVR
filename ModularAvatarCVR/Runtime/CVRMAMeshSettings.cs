using UnityEngine;

namespace ModularAvatarCVR
{
    public enum CVRMAInheritMode
    {
        /// <summary>Inherit from the nearest parent CVRMAMeshSettings.</summary>
        Inherit,
        /// <summary>Set this value explicitly.</summary>
        Set,
        /// <summary>Do not override; leave the renderer's existing value.</summary>
        DontSet,
        /// <summary>Set if no parent overrides, otherwise inherit.</summary>
        SetOrInherit
    }

    /// <summary>
    /// Applies probe anchor and bounds settings to all SkinnedMeshRenderers in this
    /// subtree at build time. Mirrors MA Mesh Settings behaviour for CVR.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Mesh Settings")]
    [DisallowMultipleComponent]
    public class CVRMAMeshSettings : CVRMAComponent
    {
        [Header("Probe Anchor")]
        public CVRMAInheritMode inheritProbeAnchor = CVRMAInheritMode.DontSet;
        [Tooltip("Transform to use as the probe anchor for all renderers in this subtree.")]
        public Transform probeAnchor;

        [Header("Bounds")]
        public CVRMAInheritMode inheritBounds = CVRMAInheritMode.DontSet;
        [Tooltip("Root bone for bounds calculation.")]
        public Transform rootBone;
        public Bounds localBounds = new Bounds(Vector3.zero, Vector3.one * 2f);
    }
}
