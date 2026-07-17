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
    /// Reparents this object to a humanoid bone (or a path under it) at build time.
    /// Mirrors VRC MA's Bone Proxy: the target is identified by a HumanBodyBones
    /// reference rather than a direct Transform, so the same setup works on any
    /// humanoid avatar regardless of bone naming.
    ///
    /// If <see cref="boneReference"/> is <see cref="HumanBodyBones.LastBone"/> (shown
    /// as "None" in the inspector), <see cref="subPath"/> is resolved from the avatar
    /// root instead of from a bone.
    /// In editor, mirrors the resolved target's transform for preview.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Bone Proxy")]
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public class CVRMABoneProxy : CVRMAComponent
    {
        [Tooltip("Humanoid bone to attach to. Set to 'None' (LastBone) to resolve Sub Path from the avatar root instead.")]
        public HumanBodyBones boneReference = HumanBodyBones.LastBone;

        [Tooltip("Optional path under the referenced bone (or under the avatar root when Bone Reference is None).")]
        public string subPath = "";

        [Tooltip("How to place this object relative to the target bone.")]
        public CVRMABoneProxyAttachment attachmentMode = CVRMABoneProxyAttachment.AsChildAtRoot;

        [Tooltip("Match scale of this object to the target bone.")]
        public bool matchScale = false;

        /// <summary>Resolves the target transform using the avatar's humanoid animator (found by walking up).</summary>
        public Transform ResolveTarget() => ResolveTarget(FindAvatarAnimator());

        /// <summary>Resolves the target transform against a specific avatar animator.</summary>
        public Transform ResolveTarget(Animator avatarAnimator)
        {
            Transform baseTransform;

            if (boneReference == HumanBodyBones.LastBone)
            {
                baseTransform = avatarAnimator != null ? avatarAnimator.transform : null;
                if (baseTransform == null) return null;
            }
            else
            {
                if (avatarAnimator == null || !avatarAnimator.isHuman) return null;
                baseTransform = avatarAnimator.GetBoneTransform(boneReference);
                if (baseTransform == null) return null;
            }

            if (string.IsNullOrEmpty(subPath)) return baseTransform;
            return baseTransform.Find(subPath);
        }

        /// <summary>Finds the nearest humanoid Animator above this component (the avatar's).</summary>
        public Animator FindAvatarAnimator() => GetComponentInParent<Animator>(true);

#if UNITY_EDITOR
        private void Update()
        {
            if (Application.isPlaying) return;

            var resolvedTarget = ResolveTarget();
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
                // AsChildKeepWorldPose: leave the object where it is.
            }

            if (matchScale)
                transform.localScale = resolvedTarget.localScale;
        }
#endif
    }
}
