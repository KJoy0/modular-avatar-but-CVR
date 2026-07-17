#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    internal static class CVRMASetupOutfit
    {
        [MenuItem("GameObject/Modular Avatar CVR/Setup Outfit", false, 20)]
        private static void SetupOutfit(MenuCommand command)
        {
            var outfitRoot = command.context as GameObject;
            if (outfitRoot == null) return;

            // Find avatar root by walking up from the outfit's parent
            var avatarComp = outfitRoot.transform.parent != null
                ? outfitRoot.transform.parent.GetComponentInParent<ABI.CCK.Components.CVRAvatar>(true)
                : null;

            if (avatarComp == null)
            {
                EditorUtility.DisplayDialog("Setup Outfit",
                    $"'{outfitRoot.name}' must be a child of an avatar that has a CVRAvatar component.", "OK");
                return;
            }

            var avatarRoot = avatarComp.gameObject;

            var avatarArmature = FindArmature(avatarRoot.transform);
            if (avatarArmature == null)
            {
                EditorUtility.DisplayDialog("Setup Outfit",
                    "Could not find the avatar's armature. " +
                    "Make sure the avatar has a child named 'Armature' or a bone hierarchy.", "OK");
                return;
            }

            var outfitArmature = FindArmature(outfitRoot.transform);
            if (outfitArmature == null)
            {
                EditorUtility.DisplayDialog("Setup Outfit",
                    $"Could not find an armature under '{outfitRoot.name}'. " +
                    "Make sure the outfit has a bone hierarchy (a child named 'Armature' or similar).", "OK");
                return;
            }

            var existing = outfitArmature.GetComponent<CVRMAMergeArmature>();
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Setup Outfit",
                    $"'{outfitArmature.name}' already has a CVRMAMergeArmature component. Replace it?",
                    "Replace", "Cancel"))
                    return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Setup Outfit");
            int group = Undo.GetCurrentGroup();

            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            // Avoid armature-name collision: animation paths get ambiguous when the
            // outfit's armature object has the same name as the avatar's.
            if (outfitArmature.name == avatarArmature.name)
            {
                Undo.RecordObject(outfitArmature.gameObject, "Rename outfit armature");
                outfitArmature.name += ".1";
            }

            var merge = Undo.AddComponent<CVRMAMergeArmature>(outfitArmature.gameObject);
            merge.mergeTarget = avatarArmature;

            CVRMAArmatureUtil.DetectPrefixSuffix(outfitArmature, avatarArmature, out var prefix, out var suffix);
            merge.prefix      = prefix;
            merge.suffix      = suffix;
            merge.mangleNames = !string.IsNullOrEmpty(prefix) || !string.IsNullOrEmpty(suffix);

            SetupMeshSettings(outfitRoot, avatarRoot);

            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(outfitArmature.gameObject);

            var affixNote = string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix)
                ? ""
                : $" (prefix='{prefix}', suffix='{suffix}')";
            Debug.Log($"[MA-CVR] Setup Outfit: attached MergeArmature on '{outfitArmature.name}' " +
                      $"→ '{avatarArmature.name}'{affixNote}");
        }

        [MenuItem("GameObject/Modular Avatar CVR/Setup Outfit", true)]
        private static bool ValidateSetupOutfit()
        {
            return Selection.activeGameObject != null && !EditorApplication.isPlaying;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Adds a CVRMAMeshSettings to the outfit root configured from the base avatar's
        /// renderers, so outfit meshes light and cull consistently with the body:
        /// probe anchor and root bone are copied when the avatar's own meshes agree on them.
        /// </summary>
        private static void SetupMeshSettings(GameObject outfitRoot, GameObject avatarRoot)
        {
            if (outfitRoot.GetComponent<CVRMAMeshSettings>() != null) return;
            if (outfitRoot.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) return;

            // Gather the base avatar's own renderers (excluding the outfit's).
            Transform probeAnchor = null;
            Transform rootBone = null;
            bool probeConsistent = true, rootConsistent = true, first = true;

            foreach (var smr in avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.transform.IsChildOf(outfitRoot.transform)) continue;

                if (first)
                {
                    probeAnchor = smr.probeAnchor;
                    rootBone    = smr.rootBone;
                    first = false;
                    continue;
                }

                if (smr.probeAnchor != probeAnchor) probeConsistent = false;
                if (smr.rootBone    != rootBone)    rootConsistent  = false;
            }

            if (first) return; // avatar has no renderers of its own — nothing to inherit

            var settings = Undo.AddComponent<CVRMAMeshSettings>(outfitRoot);
            bool applied = false;

            if (probeConsistent && probeAnchor != null)
            {
                settings.inheritProbeAnchor = CVRMAInheritMode.Set;
                settings.probeAnchor = probeAnchor;
                applied = true;
            }

            if (rootConsistent && rootBone != null)
            {
                settings.inheritBounds = CVRMAInheritMode.Set;
                settings.rootBone = rootBone;
                settings.localBounds = new Bounds(Vector3.zero, Vector3.one * 2f);
                applied = true;
            }

            if (!applied)
            {
                // Nothing consistent to copy — remove the empty component again.
                Undo.DestroyObjectImmediate(settings);
            }
        }

        /// <summary>
        /// Finds the armature under <paramref name="root"/>: prefers a child named
        /// "Armature" (case-insensitive), then falls back to the first child that
        /// has children of its own and is not a SkinnedMeshRenderer.
        /// </summary>
        private static Transform FindArmature(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name.Equals("Armature", System.StringComparison.OrdinalIgnoreCase))
                    return child;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.childCount > 0 && child.GetComponent<SkinnedMeshRenderer>() == null
                                         && child.GetComponent<MeshRenderer>() == null)
                    return child;
            }

            return null;
        }

    }
}
#endif
