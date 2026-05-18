using UnityEngine;

namespace ModularAvatarCVR
{
    public enum CVRMArmatureLockMode
    {
        /// <summary>Move merge bones to match base bones every frame in editor.</summary>
        BaseToMerge,
        /// <summary>Bones mirror each other bidirectionally in editor.</summary>
        BidirectionalExact,
        /// <summary>No editor-time locking; bones are only merged at build time.</summary>
        NotLocked
    }

    /// <summary>
    /// Merges this object's armature into the avatar's base armature at build time.
    /// Bones are matched by name after stripping the configured prefix/suffix.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Merge Armature")]
    [DisallowMultipleComponent]
    public class CVRMAMergeArmature : CVRMAComponent
    {
        [Tooltip("Root bone of the avatar armature to merge into.")]
        public Transform mergeTarget;

        [Tooltip("Prefix on bone names in this armature to strip when matching.")]
        public string prefix = "";

        [Tooltip("Suffix on bone names in this armature to strip when matching.")]
        public string suffix = "";

        [Tooltip("Rename bones in this armature to match the base armature after merging.")]
        public bool mangleNames = true;

        public CVRMArmatureLockMode lockMode = CVRMArmatureLockMode.BaseToMerge;

        private void Update()
        {
            if (Application.isPlaying) return;
            if (lockMode == CVRMArmatureLockMode.NotLocked) return;
            if (mergeTarget == null) return;

            SyncBones(transform, mergeTarget);
        }

        private void SyncBones(Transform merge, Transform baseBone)
        {
            foreach (Transform child in merge)
            {
                var matched = FindCorrespondingBone(child, baseBone);
                if (matched == null) continue;

                if (lockMode == CVRMArmatureLockMode.BaseToMerge)
                {
                    child.position = matched.position;
                    child.rotation = matched.rotation;
                }
                else if (lockMode == CVRMArmatureLockMode.BidirectionalExact)
                {
                    child.position = matched.position;
                    child.rotation = matched.rotation;
                    matched.position = child.position;
                    matched.rotation = child.rotation;
                }

                SyncBones(child, matched);
            }
        }

        public Transform FindCorrespondingBone(Transform bone, Transform baseParent)
        {
            var name = bone.name;
            if (!name.StartsWith(prefix) || !name.EndsWith(suffix)) return null;
            if (name.Length == prefix.Length + suffix.Length) return null;
            var targetName = name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length);
            return baseParent.Find(targetName);
        }
    }
}
