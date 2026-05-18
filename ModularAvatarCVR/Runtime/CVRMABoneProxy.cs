using UnityEngine;

namespace ModularAvatarCVR
{
    public enum CVRMABoneProxyAttachment
    {
        /// <summary>Place at target with zeroed local position/rotation.</summary>
        AsChildAtRoot,
        /// <summary>Place at target, keeping current world pose.</summary>
        AsChildKeepWorldPose,
        /// <summary>Place at target, keeping current local rotation.</summary>
        AsChildKeepRotation,
        /// <summary>Place at target, keeping current local position.</summary>
        AsChildKeepPosition
    }

    /// <summary>
    /// Reparents this object to a target bone at build time.
    /// In editor, mirrors the target bone's transform for preview.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Bone Proxy")]
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public class CVRMABoneProxy : CVRMAComponent
    {
        [Tooltip("The bone this object should be attached to.")]
        public Transform target;

        [Tooltip("If set, also navigate this sub-path under the target bone.")]
        public string subPath = "";

        [Tooltip("How to place this object relative to the target bone.")]
        public CVRMABoneProxyAttachment attachmentMode = CVRMABoneProxyAttachment.AsChildAtRoot;

        [Tooltip("Match scale of this object to the target bone.")]
        public bool matchScale = false;

        private void Update()
        {
            if (Application.isPlaying) return;
            if (target == null) return;

            var resolvedTarget = string.IsNullOrEmpty(subPath) ? target : target.Find(subPath);
            if (resolvedTarget == null) return;

            switch (attachmentMode)
            {
                case CVRMABoneProxyAttachment.AsChildAtRoot:
                    transform.position = resolvedTarget.position;
                    transform.rotation = resolvedTarget.rotation;
                    break;
                case CVRMABoneProxyAttachment.AsChildKeepPosition:
                    transform.rotation = resolvedTarget.rotation;
                    break;
                case CVRMABoneProxyAttachment.AsChildKeepRotation:
                    transform.position = resolvedTarget.position;
                    break;
            }

            if (matchScale)
                transform.localScale = resolvedTarget.localScale;
        }
    }
}
