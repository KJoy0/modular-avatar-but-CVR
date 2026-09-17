#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Runs the full MA-CVR build pipeline on a throwaway copy of the avatar in the
    /// scene, so the processed result can be inspected without uploading — the CVR
    /// equivalent of VRC MA's "Manual bake avatar".
    ///
    /// Passes normally write their generated assets (cloned meshes, controllers, clips)
    /// into the shared temp folder, which is wiped after every upload. A bake moves them
    /// into their own folder under <see cref="BakeRoot"/> instead; asset GUIDs survive a
    /// move, so the baked copy keeps working until you delete it.
    /// </summary>
    internal static class CVRMAManualBake
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";
        private const string BakeRoot   = "Assets/MA_CVR_Baked";

        [MenuItem("Tools/Modular Avatar CVR/Manual Bake Avatar", false, 30)]
        private static void BakeSelectedAvatar()
        {
            var source = FindAvatar(Selection.activeGameObject);
            if (source == null)
            {
                EditorUtility.DisplayDialog("Modular Avatar CVR",
                    "Select an avatar first — an object with a CVRAvatar component, or any child of one.",
                    "OK");
                return;
            }

            if (source.GetComponentInChildren<CVRMAComponent>(true) == null)
            {
                EditorUtility.DisplayDialog("Modular Avatar CVR",
                    $"'{source.name}' has no Modular Avatar CVR components, so a bake would do nothing.",
                    "OK");
                return;
            }

            Bake(source);
        }

        [MenuItem("Tools/Modular Avatar CVR/Manual Bake Avatar", true)]
        private static bool ValidateBakeSelectedAvatar() =>
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            FindAvatar(Selection.activeGameObject) != null;

        /// <summary>
        /// Duplicates the avatar, runs every build pass on the copy, and selects it.
        /// Returns the baked copy, or null if the bake failed.
        /// </summary>
        internal static GameObject Bake(GameObject source)
        {
            // Editor previews mutate the scene (weights, active states, materials).
            // They must be released BEFORE duplicating or they'd be baked in as authored.
            CVRMAReactivePreview.RestoreForBuild();

            var preExistingAssets = SnapshotTempAssets();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("MA-CVR Manual Bake");

            var copy = UnityEngine.Object.Instantiate(source, source.transform.parent, true);
            copy.name = source.name + " (MA-CVR Baked)";
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(copy, "MA-CVR Manual Bake");

            // Passes delete components, which a prefab instance would refuse.
            if (PrefabUtility.IsPartOfPrefabInstance(copy))
                PrefabUtility.UnpackPrefabInstance(
                    copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            try
            {
                new CVRMABuildProcessor().OnPreProcessAvatar(copy);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Undo.DestroyObjectImmediate(copy);
                Undo.CollapseUndoOperations(undoGroup);
                EditorUtility.DisplayDialog("Modular Avatar CVR",
                    "The bake failed — see the Console for the error.\n\n" +
                    "The half-processed copy was removed; your original avatar is untouched.",
                    "OK");
                return null;
            }

            // Hide the original so you aren't inspecting two overlapping avatars.
            Undo.RecordObject(source, "MA-CVR Manual Bake");
            source.SetActive(false);
            Undo.CollapseUndoOperations(undoGroup);

            var folder = RelocateGeneratedAssets(preExistingAssets, source.name);

            Selection.activeGameObject = copy;
            EditorGUIUtility.PingObject(copy);

            Debug.Log(
                $"[MA-CVR] Manual bake complete: '{copy.name}'. The original was hidden — undo (Ctrl+Z) " +
                "reverses the whole bake." +
                (folder != null ? $" Generated assets: {folder}" : ""), copy);

            return copy;
        }

        // ------------------------------------------------------------------ assets

        private static HashSet<string> SnapshotTempAssets()
        {
            var paths = new HashSet<string>();
            if (!AssetDatabase.IsValidFolder(TempFolder)) return paths;

            foreach (var guid in AssetDatabase.FindAssets("", new[] { TempFolder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            return paths;
        }

        /// <summary>
        /// Moves everything the bake just generated out of the temp folder (which the next
        /// upload deletes) into its own dated folder. Moves preserve GUIDs, so the baked
        /// copy's references follow along. Returns the folder, or null if nothing moved.
        /// </summary>
        private static string RelocateGeneratedAssets(HashSet<string> preExisting, string avatarName)
        {
            if (!AssetDatabase.IsValidFolder(TempFolder)) return null;

            var generated = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { TempFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!preExisting.Contains(path) && !AssetDatabase.IsValidFolder(path))
                    generated.Add(path);
            }
            if (generated.Count == 0) return null;

            if (!AssetDatabase.IsValidFolder(BakeRoot))
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(BakeRoot));

            var folderName = $"{Sanitize(avatarName)} {DateTime.Now:yyyy-MM-dd HH-mm-ss}";
            var folder = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(BakeRoot, folderName));

            foreach (var path in generated)
            {
                var error = AssetDatabase.MoveAsset(path, $"{folder}/{Path.GetFileName(path)}");
                if (!string.IsNullOrEmpty(error))
                    Debug.LogWarning($"[MA-CVR] Manual bake: couldn't move '{path}' — {error}");
            }

            // Clears the merge pass's tracked paths (now stale) and drops the empty temp folder.
            CVRMAMergeAnimatorPass.Cleanup();
            AssetDatabase.SaveAssets();
            return folder;
        }

        [MenuItem("Tools/Modular Avatar CVR/Clean Up Baked Assets", false, 31)]
        private static void CleanUpBakedAssets()
        {
            if (!AssetDatabase.IsValidFolder(BakeRoot))
            {
                EditorUtility.DisplayDialog("Modular Avatar CVR", "There are no baked assets to clean up.", "OK");
                return;
            }

            var folders = AssetDatabase.GetSubFolders(BakeRoot);
            if (!EditorUtility.DisplayDialog("Modular Avatar CVR",
                    $"Delete {folders.Length} baked asset folder(s) under {BakeRoot}?\n\n" +
                    "Any baked avatar copies still in a scene will lose their generated meshes " +
                    "and animators. Your original avatars are unaffected.",
                    "Delete", "Cancel"))
                return;

            AssetDatabase.DeleteAsset(BakeRoot);
            AssetDatabase.Refresh();
            Debug.Log($"[MA-CVR] Deleted baked assets under {BakeRoot}.");
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject FindAvatar(GameObject from)
        {
            var t = from != null ? from.transform : null;
            while (t != null)
            {
                if (t.GetComponent<ABI.CCK.Components.CVRAvatar>() != null) return t.gameObject;
                t = t.parent;
            }
            return null;
        }

        private static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
#endif
