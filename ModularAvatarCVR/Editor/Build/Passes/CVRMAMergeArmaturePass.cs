#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
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

                MergeHierarchy(merger, merger.transform, merger.mergeTarget);
                Object.DestroyImmediate(merger);
            }
        }

        private static void MergeHierarchy(CVRMAMergeArmature merger, Transform mergeParent, Transform baseParent)
        {
            var children = new List<Transform>();
            foreach (Transform child in mergeParent)
                children.Add(child);

            foreach (var child in children)
            {
                // Skip nested merge armatures — they handle themselves
                if (child.GetComponent<CVRMAMergeArmature>() != null) continue;

                var baseBone = merger.FindCorrespondingBone(child, baseParent);
                if (baseBone != null)
                {
                    // Reparent children of the merge bone under the base bone
                    MergeHierarchy(merger, child, baseBone);

                    // Move any non-bone children (accessories, meshes) to the base bone
                    ReparentNonBoneChildren(child, baseBone);

                    // If mangling names, we leave the original mesh skinned to this bone;
                    // the skin weights still reference the merge bone by instance, so we
                    // need to keep it alive but reparent it under the base bone.
                    child.SetParent(baseBone, true);

                    if (merger.mangleNames)
                        child.name = baseBone.name + "_MA";
                }
                else
                {
                    // No matching base bone — adopt directly under baseParent
                    child.SetParent(baseParent, true);
                }
            }
        }

        private static void ReparentNonBoneChildren(Transform source, Transform dest)
        {
            var toMove = new List<Transform>();
            foreach (Transform child in source)
            {
                // Heuristic: GameObjects with renderers or other components are accessories
                if (child.GetComponentsInChildren<Component>(true).Length > 1)
                    toMove.Add(child);
            }

            foreach (var t in toMove)
                t.SetParent(dest, true);
        }
    }
}
#endif
