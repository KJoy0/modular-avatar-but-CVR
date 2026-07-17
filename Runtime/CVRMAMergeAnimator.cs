using UnityEngine;

namespace ModularAvatarCVR
{
    public enum CVRMAPathMode
    {
        /// <summary>Animation paths are relative to this object's position in the hierarchy.</summary>
        Relative,
        /// <summary>Animation paths are absolute from the avatar root.</summary>
        Absolute
    }

    public enum CVRMAMergeMode
    {
        /// <summary>Append layers to the avatar's existing animator.</summary>
        Append,
        /// <summary>Replace the avatar's entire animator with this controller.</summary>
        Replace
    }

    /// <summary>
    /// Merges an AnimatorController into the avatar's override controller at build time.
    /// Supports relative-path remapping so accessories can be self-contained.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Merge Animator")]
    public class CVRMAMergeAnimator : CVRMAComponent
    {
        [Tooltip("The AnimatorController whose layers will be merged into the avatar.")]
        public RuntimeAnimatorController animator;

        public CVRMAPathMode pathMode = CVRMAPathMode.Relative;
        public CVRMAMergeMode mergeMode = CVRMAMergeMode.Append;

        [Tooltip("If true, deletes the Animator component on this GameObject after merging.")]
        public bool deleteAttachedAnimator = true;

        [Tooltip("Match the avatar's Write Defaults setting on all merged states.")]
        public bool matchAvatarWriteDefaults = true;

        [Tooltip("Lower numbers are added before higher numbers when multiple merge animators exist.")]
        public int layerPriority = 0;
    }
}
