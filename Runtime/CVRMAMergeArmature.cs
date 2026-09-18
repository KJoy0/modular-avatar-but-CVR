using UnityEngine;

namespace ModularAvatarCVR
{
    public enum CVRMArmatureLockMode
    {
        /// <summary>Merge bones follow base-bone movement in editor (offsets preserved).</summary>
        BaseToMerge,
        /// <summary>Bones mirror each other bidirectionally in editor (offsets preserved).</summary>
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

        [Tooltip("Remove colliders this outfit duplicates from the avatar — common when the outfit " +
                 "armature was copied from it, which otherwise leaves two identical colliders on " +
                 "every bone. Anything referencing the outfit's copy is repointed at the avatar's.")]
        public bool removeDuplicateColliders = true;

        [Tooltip("Editor-only bone locking, mirroring VRC MA: each bone pair's offset is captured " +
                 "when the lock engages and preserved, so locking never deforms the outfit. " +
                 "Base → Merge: outfit bones follow avatar bone movement. Bidirectional: either side follows the other.")]
        public CVRMArmatureLockMode lockMode = CVRMArmatureLockMode.NotLocked;

#if UNITY_EDITOR
        /// <summary>
        /// One locked bone pair. The offset (merge relative to base, captured when the
        /// lock engages) is PRESERVED while syncing — the outfit follows movement but
        /// keeps its authored pose, so locking never deforms a not-perfectly-aligned rig.
        /// </summary>
        private struct LockEntry
        {
            public Transform mergeBone;
            public Transform baseBone;
            public Vector3 posOffset;       // merge position in the base bone's frame
            public Quaternion rotOffset;    // merge rotation relative to the base bone
            public Vector3 lastMergePos;
            public Quaternion lastMergeRot;
            public Vector3 lastBasePos;
            public Quaternion lastBaseRot;
        }

        private readonly System.Collections.Generic.List<LockEntry> _lockEntries =
            new System.Collections.Generic.List<LockEntry>();
        private CVRMArmatureLockMode _capturedMode = CVRMArmatureLockMode.NotLocked;
        private Transform _capturedTarget;

        private void Update()
        {
            if (Application.isPlaying) return;
            if (lockMode == CVRMArmatureLockMode.NotLocked)
            {
                _capturedMode = lockMode;
                _lockEntries.Clear();
                return;
            }
            if (mergeTarget == null) return;

            if (_capturedMode != lockMode || _capturedTarget != mergeTarget || _lockEntries.Count == 0)
                CaptureLockState();

            for (int i = 0; i < _lockEntries.Count; i++)
            {
                var e = _lockEntries[i];
                if (e.mergeBone == null || e.baseBone == null) continue;

                if (lockMode == CVRMArmatureLockMode.BaseToMerge)
                {
                    var targetPos = e.baseBone.position + e.baseBone.rotation * e.posOffset;
                    var targetRot = e.baseBone.rotation * e.rotOffset;
                    if (e.mergeBone.position != targetPos) e.mergeBone.position = targetPos;
                    if (e.mergeBone.rotation != targetRot) e.mergeBone.rotation = targetRot;
                }
                else // BidirectionalExact
                {
                    bool mergeMoved = e.mergeBone.position != e.lastMergePos ||
                                      e.mergeBone.rotation != e.lastMergeRot;
                    bool baseMoved  = e.baseBone.position != e.lastBasePos ||
                                      e.baseBone.rotation != e.lastBaseRot;

                    if (mergeMoved && !baseMoved)
                    {
                        // User moved the outfit bone — drag the base bone along, offset intact.
                        e.baseBone.rotation = e.mergeBone.rotation * Quaternion.Inverse(e.rotOffset);
                        e.baseBone.position = e.mergeBone.position - e.baseBone.rotation * e.posOffset;
                    }
                    else if (baseMoved)
                    {
                        e.mergeBone.position = e.baseBone.position + e.baseBone.rotation * e.posOffset;
                        e.mergeBone.rotation = e.baseBone.rotation * e.rotOffset;
                    }

                    e.lastMergePos = e.mergeBone.position;
                    e.lastMergeRot = e.mergeBone.rotation;
                    e.lastBasePos  = e.baseBone.position;
                    e.lastBaseRot  = e.baseBone.rotation;
                    _lockEntries[i] = e;
                }
            }
        }

        private void CaptureLockState()
        {
            _lockEntries.Clear();
            _capturedMode = lockMode;
            _capturedTarget = mergeTarget;
            CaptureRecursive(transform, mergeTarget);
        }

        private void CaptureRecursive(Transform merge, Transform baseParent)
        {
            foreach (Transform child in merge)
            {
                var matched = FindCorrespondingBone(child, baseParent);
                if (matched == null) continue;

                _lockEntries.Add(new LockEntry
                {
                    mergeBone    = child,
                    baseBone     = matched,
                    posOffset    = Quaternion.Inverse(matched.rotation) * (child.position - matched.position),
                    rotOffset    = Quaternion.Inverse(matched.rotation) * child.rotation,
                    lastMergePos = child.position,
                    lastMergeRot = child.rotation,
                    lastBasePos  = matched.position,
                    lastBaseRot  = matched.rotation
                });

                CaptureRecursive(child, matched);
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            // Inspector change (mode, prefix/suffix, target): re-capture offsets fresh.
            _lockEntries.Clear();
        }
#endif

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
