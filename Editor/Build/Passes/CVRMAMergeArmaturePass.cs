#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
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
    ///
    /// Components that merely REFERENCE an eliminated bone are handled separately: Magica Cloth
    /// points at its root bones from the outfit root rather than from the bones themselves, so
    /// those bones look empty and would be removed with the reference left dangling. Every such
    /// reference is repointed at the base bone before anything is destroyed — see
    /// CVRMAReferenceRemapUtil.
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

                // 2a. An outfit armature copied from the avatar brings the avatar's collider
                //     objects along with it. Those carry a component, so they would be kept and
                //     reparented to sit uselessly beside the avatar's originals. Drop the outfit's
                //     copies and point anything referencing them at the avatar's equivalents.
                var duplicateColliders = new List<Component>();
                var emptiedColliderObjects = new List<GameObject>();
                var replacements = new Dictionary<Object, Object>();
                if (merger.removeDuplicateColliders)
                    FindDuplicateColliders(merger, avatarRoot, map, replacements,
                        duplicateColliders, emptiedColliderObjects);

                // 2b. A matched bone is redundant once nothing but its Transform is left on it.
                var doomedComponents = new HashSet<Component>(duplicateColliders);
                var eliminate = new HashSet<Transform>();
                foreach (var kv in map)
                {
                    int surviving = 0;
                    foreach (var component in kv.Key.GetComponents<Component>())
                        if (component != null && !doomedComponents.Contains(component)) surviving++;

                    if (surviving <= 1) eliminate.Add(kv.Key); // only the Transform remains
                }

                // 3. Re-skin renderers: any bone reference pointing at an eliminated outfit bone
                //    is repointed to the base bone. Kept bones are left alone.
                RemapRenderers(avatarRoot, map, eliminate);

                // 3b. Repoint every OTHER component reference off the bones we are about to
                //     destroy — Magica Cloth root bones, collider symmetry targets, renderer probe
                //     anchors, constraints, MA-CVR components. Nothing else in the package does
                //     this, so without it those references are simply left null after the build.
                //
                //     ORDER IS LOAD-BEARING: this must run AFTER RemapRenderers (which needs the
                //     doomed bone's localToWorldMatrix for its bindpose correction, and whose
                //     remapIndices would come back empty if the bones were already repointed —
                //     silently reintroducing in-game outfit deformation) and BEFORE MergeHierarchy
                //     destroys anything.
                foreach (var doomed in eliminate) replacements[doomed] = map[doomed];

                if (replacements.Count > 0)
                {
                    int repointed = CVRMAReferenceRemapUtil.RemapReferences(
                        avatarRoot, replacements, "MergeArmature");
                    if (repointed > 0)
                        Debug.Log(
                            $"[MA-CVR] MergeArmature on '{merger.gameObject.name}': repointed " +
                            $"{repointed} reference(s) off {eliminate.Count} removed bone(s) and " +
                            $"{duplicateColliders.Count} duplicate collider(s).");
                }

                // 3c. Nothing points at them any more, so the duplicates can go — the colliders
                //     first, then any object left holding nothing but its Transform.
                foreach (var collider in duplicateColliders)
                    if (collider != null) Object.DestroyImmediate(collider);

                foreach (var emptied in emptiedColliderObjects)
                    if (emptied != null) Object.DestroyImmediate(emptied);

                if (duplicateColliders.Count > 0)
                    Debug.Log(
                        $"[MA-CVR] MergeArmature on '{merger.gameObject.name}': removed " +
                        $"{duplicateColliders.Count} collider(s) the avatar already provides" +
                        (emptiedColliderObjects.Count > 0
                            ? $", and {emptiedColliderObjects.Count} object(s) left empty by that."
                            : "."));

                // 4. Restructure the hierarchy: eliminate redundant bones, reparent kept bones
                //    and unmatched objects under the correct base bone (world pose preserved).
                MergeHierarchy(merger, merger.transform, merger.mergeTarget, map, eliminate);

                Object.DestroyImmediate(merger);
            }
        }

        /// <summary>
        /// Finds colliders the outfit duplicates from the avatar and maps each to the avatar's
        /// equivalent, so references survive the deletion.
        ///
        /// Matching is by object name (affixes stripped) plus component type, rather than by the
        /// bone map: an outfit's collider objects frequently do NOT line up with the avatar's
        /// hierarchy, so they never enter the bone map and instead get adopted as siblings — which
        /// is how duplicates kept appearing even after matched ones were being handled.
        ///
        /// Colliders ONLY. An outfit's cloth/dynamics component is never a duplicate even when the
        /// avatar has one of the same type — it drives different meshes, and removing it would
        /// break the outfit.
        /// </summary>
        private static void FindDuplicateColliders(
            CVRMAMergeArmature merger,
            GameObject avatarRoot,
            Dictionary<Transform, Transform> map,
            Dictionary<Object, Object> replacements,
            List<Component> doomedColliders,
            List<GameObject> doomedObjects)
        {
            // Index what the avatar already provides. Anything still sitting under a merge
            // armature belongs to an outfit — including outfits queued behind this one — and
            // must not be treated as the survivor, since it may itself be removed later.
            var existing = new Dictionary<(string, System.Type), Component>();
            foreach (var candidate in avatarRoot.GetComponentsInChildren<Component>(true))
            {
                if (candidate == null || !IsCollider(candidate)) continue;
                if (candidate.GetComponentInParent<CVRMAMergeArmature>(true) != null) continue;

                var key = (StripAffixes(candidate.gameObject.name, merger), candidate.GetType());
                if (!existing.ContainsKey(key)) existing[key] = candidate;
            }
            if (existing.Count == 0) return;

            foreach (var outfitCollider in merger.GetComponentsInChildren<Component>(true))
            {
                if (outfitCollider == null || !IsCollider(outfitCollider)) continue;

                var key = (StripAffixes(outfitCollider.gameObject.name, merger), outfitCollider.GetType());
                if (!existing.TryGetValue(key, out var avatarCollider)) continue;
                if (avatarCollider == outfitCollider) continue;

                replacements[outfitCollider] = avatarCollider;
                doomedColliders.Add(outfitCollider);
            }

            // An object that existed only to carry a duplicated collider is clutter once the
            // collider is gone. Matched ones are already covered by the eliminate set; drop the
            // unmatched ones here, pointing references at the avatar's equivalent object.
            var doomedSet = new HashSet<Component>(doomedColliders);
            foreach (var group in doomedColliders)
            {
                var owner = group.gameObject;
                if (map.ContainsKey(owner.transform)) continue;   // handled via eliminate
                if (owner.transform.childCount > 0) continue;     // still holds something
                if (doomedObjects.Contains(owner)) continue;

                int surviving = 0;
                foreach (var component in owner.GetComponents<Component>())
                    if (component != null && !doomedSet.Contains(component)) surviving++;
                if (surviving > 1) continue;                      // more than just the Transform

                doomedObjects.Add(owner);
                if (replacements[group] is Component survivor)
                    replacements[owner.transform] = survivor.transform;
            }
        }

        /// <summary>Removes the merger's configured prefix/suffix from a name, when present.</summary>
        private static string StripAffixes(string name, CVRMAMergeArmature merger)
        {
            if (!string.IsNullOrEmpty(merger.prefix) && name.StartsWith(merger.prefix))
                name = name.Substring(merger.prefix.Length);
            if (!string.IsNullOrEmpty(merger.suffix) && name.EndsWith(merger.suffix))
                name = name.Substring(0, name.Length - merger.suffix.Length);
            return name;
        }

        /// <summary>
        /// Type-free collider test: Unity's own colliders, plus anything whose type name says so
        /// (MagicaCapsuleCollider, MagicaSphereCollider, VRCPhysBoneCollider…). Kept name-based so
        /// the package needs no compile-time dependency on Magica or any other dynamics system.
        /// </summary>
        private static bool IsCollider(Component component) =>
            component is Collider ||
            component.GetType().Name.IndexOf("Collider", System.StringComparison.OrdinalIgnoreCase) >= 0;

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

        private const string TempFolder = "Assets/MA_CVR_Temp";

        private static void RemapRenderers(GameObject avatarRoot,
            Dictionary<Transform, Transform> map, HashSet<Transform> eliminate)
        {
            // mesh → bindpose-corrected clone, so shared meshes are processed once
            // (renderers sharing a mesh share its rig, so the correction is identical).
            var correctedMeshes = new Dictionary<Mesh, Mesh>();
            bool savedAny = false;

            foreach (var smr in avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = smr.bones;
                var remapIndices = new List<int>();
                for (int i = 0; i < bones.Length; i++)
                    if (bones[i] != null && eliminate.Contains(bones[i]))
                        remapIndices.Add(i);

                if (remapIndices.Count > 0)
                {
                    // Re-skinning onto a bone with a different orientation/scale is only
                    // correct if the bindpose is adjusted to compensate — otherwise the
                    // mesh deforms in-game by exactly the orientation difference:
                    //   newBind = baseBone⁻¹ × outfitBone × oldBind
                    var mesh = smr.sharedMesh;
                    if (mesh != null && !correctedMeshes.TryGetValue(mesh, out var corrected))
                    {
                        var bind = mesh.bindposes;
                        if (bind.Length == bones.Length)
                        {
                            foreach (var i in remapIndices)
                            {
                                var outfitBone = bones[i];
                                var baseBone = map[outfitBone];
                                bind[i] = baseBone.worldToLocalMatrix * outfitBone.localToWorldMatrix * bind[i];
                            }

                            corrected = Object.Instantiate(mesh);
                            corrected.name = mesh.name + "_Merged";
                            corrected.bindposes = bind;

                            EnsureTempFolder();
                            AssetDatabase.CreateAsset(corrected,
                                $"{TempFolder}/MA_CVR_{corrected.name}_{GUID.Generate()}.asset");
                            savedAny = true;
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[MA-CVR] MergeArmature: '{smr.name}' bindpose count doesn't match its " +
                                "bone count — re-skinned without bindpose correction (may deform).");
                            corrected = mesh; // fall back to the uncorrected mesh
                        }
                        correctedMeshes[mesh] = corrected;
                    }

                    if (mesh != null && correctedMeshes[mesh] != mesh)
                        smr.sharedMesh = correctedMeshes[mesh];

                    foreach (var i in remapIndices)
                        bones[i] = map[bones[i]];
                    smr.bones = bones;
                }

                if (smr.rootBone != null && eliminate.Contains(smr.rootBone))
                    smr.rootBone = map[smr.rootBone];
            }

            if (savedAny) AssetDatabase.SaveAssets();
        }

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "MA_CVR_Temp");
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
