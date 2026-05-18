#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    internal static class CVRMASetupOutfit
    {
        [MenuItem("GameObject/[ModularAvatar CVR] Setup Outfit", false, 20)]
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

            var merge = Undo.AddComponent<CVRMAMergeArmature>(outfitArmature.gameObject);
            merge.mergeTarget = avatarArmature;

            DetectPrefixSuffix(outfitArmature, avatarArmature, out var prefix, out var suffix);
            merge.prefix      = prefix;
            merge.suffix      = suffix;
            merge.mangleNames = !string.IsNullOrEmpty(prefix) || !string.IsNullOrEmpty(suffix);

            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(outfitArmature.gameObject);

            var affixNote = string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix)
                ? ""
                : $" (prefix='{prefix}', suffix='{suffix}')";
            Debug.Log($"[MA-CVR] Setup Outfit: attached MergeArmature on '{outfitArmature.name}' " +
                      $"→ '{avatarArmature.name}'{affixNote}");
        }

        [MenuItem("GameObject/[ModularAvatar CVR] Setup Outfit", true)]
        private static bool ValidateSetupOutfit()
        {
            return Selection.activeGameObject != null && !EditorApplication.isPlaying;
        }

        // ------------------------------------------------------------------ helpers

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

        /// <summary>
        /// Compares bone names between the outfit armature and the avatar armature to
        /// detect a common prefix or suffix added to all outfit bone names.
        /// Sets both to empty strings if bones already match directly.
        /// </summary>
        private static void DetectPrefixSuffix(Transform outfitArmature, Transform avatarArmature,
            out string prefix, out string suffix)
        {
            prefix = "";
            suffix = "";

            var avatarBones = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var t in avatarArmature.GetComponentsInChildren<Transform>(true))
                avatarBones.Add(t.name);

            if (avatarBones.Count == 0) return;

            foreach (var bone in outfitArmature.GetComponentsInChildren<Transform>(true))
            {
                if (bone == outfitArmature) continue;
                var name = bone.name;

                // Exact match — no affix on this bone, keep looking
                if (avatarBones.Contains(name)) continue;

                foreach (var avatarBone in avatarBones)
                {
                    // name = avatarBone + something  →  suffix
                    if (name.StartsWith(avatarBone, System.StringComparison.OrdinalIgnoreCase))
                    {
                        var candidate = name.Substring(avatarBone.Length);
                        if (!string.IsNullOrEmpty(candidate)) { suffix = candidate; return; }
                    }

                    // name = something + avatarBone  →  prefix
                    if (name.EndsWith(avatarBone, System.StringComparison.OrdinalIgnoreCase))
                    {
                        var candidate = name.Substring(0, name.Length - avatarBone.Length);
                        if (!string.IsNullOrEmpty(candidate)) { prefix = candidate; return; }
                    }
                }
            }
        }
    }
}
#endif
