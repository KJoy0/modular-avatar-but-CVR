using UnityEngine;

namespace ModularAvatarCVR
{
    /// <summary>
    /// Merges a BlendTree (or any Motion) into the avatar's animator at build time.
    /// The motion is wrapped in a single-state AnimatorController layer and handed
    /// off to the Merge Animator pass, so all path-remapping rules apply.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Merge Blend Tree")]
    public class CVRMAMergeBlendTree : CVRMAComponent
    {
        [Tooltip("The BlendTree or AnimationClip to merge.")]
        public Motion motion;

        [Tooltip("How to interpret animation binding paths.")]
        public CVRMAPathMode pathMode = CVRMAPathMode.Relative;

        [Tooltip("Layer name in the merged animator controller.")]
        public string layerName = "";

        [Tooltip("Lower numbers run before higher numbers.")]
        public int layerPriority = 0;
    }
}
