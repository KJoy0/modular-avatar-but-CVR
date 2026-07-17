#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Merges an outfit armature into the avatar's armature.
    ///
    /// For each outfit bone that corresponds to a base bone, the outfit mesh is RE-SKINNED
    /// onto the base bone and the redundant outfit bone is removed — matching VRC MA, which
    /// "minimizes the number of extra bones added". This avoids the slight deformation that
    /// occurs when an outfit bone is merely nested under a rotated / non-uniformly-scaled base
    /// bone (Unity can only approximate the resulting local scale, introducing skew).
    ///
    /// Outfit bones that carry dynamics or other components (PhysBone, Magica Cloth, colliders,
    /// constraints, renderers…) are KEPT and reparented under their corresponding base bone, so
    /// their behaviour is preserved; the mesh continues to skin to them.
    /// </summary>
    internal static class CVRMAMergeArmaturePass
    {
        internal static void Run(GameObject avatarRoot)
        {
            var mergers = new List<CVRMAMergeArmature>(
                avatarRoot.GetComponentsInChildren<CVRMAMergeArmature>(true));

            foreach (var merger in mergers)
            {
                if (merger == null) continue;
                if (merger.mergeTarget == null)
                {
                    Debug.LogWarning($"[MA-CVR] MergeArmature on '{merger.gameObject.name}' has no mergeTarget — skipped.", merger);
                    continue;
                }

                // Auto-infer prefix/suffix if the current values match nothing — so the merge
                // works without the user having to press "Auto-detect" first (like VRC MA).
                if (!HasTopLevelMatch(merger))
                {
                    CVRMAArmatureUtil.DetectPrefixSuffix(merger.transform, merger.mergeTarget,
                        out var p, out var s);
                    if (p != merger.prefix || s != merger.suffix)
                    {
                        merger.prefix = p;
                        merger.suffix = s;
                        Debug.Log($"[MA-CVR] MergeArmature on '{merger.gameObject.name}': " +
                                  $"auto-inferred prefix='{p}', suffix='{s}'.");
                    }

                    if (!HasTopLevelMatch(merger))
                    {
                        Debug.LogWarning(
                            $"[MA-CVR] MergeArmature on '{merger.gameObject.name}': no bones matched the " +
                            $"target armature even after auto-detection. Check the prefix/suffix. Skipped.", merger);
                        continue;
                    }
                }

                // 1. Map every matched outfit bone → base bone.
                var map = new Dictionary<Transform, Transform>();
                BuildBoneMap(merger, merger.transform, merger.mergeTarget, map);

                // 2. Decide which matched bones are redundant (pure Transform, no extra components)
                //    and can be eliminated; the rest are kept (they own dynamics/components).
                var eliminate = new HashSet<Transform>();
                foreach (var kv in map)
                    if (kv.Key.GetComponents<Component>().Length <= 1) // only Transform
                        eliminate.Add(kv.Key);

                // 3. Re-skin renderers: any bone reference pointing at an eliminated outfit bone
                //    is repointed to the base bone. Kept bones are left alone.
                RemapRenderers(avatarRoot, map, eliminate);

                // 4. Restructure the hierarchy: eliminate redundant bones, reparent kept bones
                //    and unmatched objects under the correct base bone (world pose preserved).
                MergeHierarchy(merger, merger.transform, merger.mergeTarget, map, eliminate);

                Object.DestroyImmediate(merger);
            }
        }

        private static bool HasTopLevelMatch(CVRMAMergeArmature merger)
        {
            foreach (Transform child in merger.transform)
                if (merger.FindCorrespondingBone(child, merger.mergeTarget) != null)
                    return true;
            return false;
        }

        private static void BuildBoneMap(CVRMAMergeArmature merger, Transform outfitParent,
            Transform baseParent, Dictionary<Transform, Transform> map)
        {
            foreach (Transform child in outfitParent)
            {
                if (child.GetComponent<CVRMAMergeArmature>() != null) continue; // nested merge handles itself

                var baseBone = merger.FindCorrespondingBone(child, baseParent);
                if (baseBone != null)
                {
                    map[child] = baseBone;
                    BuildBoneMap(merger, child, baseBone, map); // matched subtree can match deeper
                }
                // Unmatched bones aren't mapped — they're adopted in MergeHierarchy.
            }
        }

        private static void RemapRenderers(GameObject avatarRoot,
            Dictionary<Transform, Transform> map, HashSet<Transform> eliminate)
        {
            foreach (var smr in avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = smr.bones;
                bool changed = false;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] != null && eliminate.Contains(bones[i]))
                    {
                        bones[i] = map[bones[i]];
                        changed = true;
                    }
                }
                if (changed) smr.bones = bones;

                if (smr.rootBone != null && eliminate.Contains(smr.rootBone))
                    smr.rootBone = map[smr.rootBone];
            }
        }

        private static void MergeHierarchy(CVRMAMergeArmature merger, Transform outfitParent,
            Transform baseParent, Dictionary<Transform, Transform> map, HashSet<Transform> eliminate)
        {
            // Snapshot — we mutate parents while iterating.
            var children = new List<Transform>();
            foreach (Transform child in outfitParent)
                children.Add(child);

            foreach (var child in children)
            {
                if (child == null) continue;
                if (child.GetComponent<CVRMAMergeArmature>() != null) continue; // nested merge

                if (map.TryGetValue(child, out var baseBone))
                {
                    // Process descendants first (relocates accessories, eliminates deeper bones).
                    MergeHierarchy(merger, child, baseBone, map, eliminate);

                    if (eliminate.Contains(child))
                    {
                        // Pure redundant bone — mesh already re-skinned to baseBone, no children left.
                        Object.DestroyImmediate(child.gameObject);
                    }
                    else
                    {
                        // Kept bone (has dynamics/components): flatten under its base bone,
                        // preserving world pose so the mesh that still skins to it is unchanged.
                        child.SetParent(baseBone, true);
                        if (merger.mangleNames)
                            child.name = baseBone.name + "_MA";
                    }
                }
                else
                {
                    // Outfit-only object (extra bone, accessory, mesh) — adopt under the
                    // current base bone, keeping its world pose.
                    child.SetParent(baseParent, true);
                }
            }
        }
    }
}
#endif
