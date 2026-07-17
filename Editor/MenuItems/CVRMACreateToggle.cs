#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// "Create Toggle" and "Create Toggle for Selection" hierarchy menu items, mirroring
    /// VRC MA. Both create CVRMAObjectToggle GameObjects under a "Toggles" container at
    /// the avatar root. "...for Selection" creates one SEPARATE toggle per selected object,
    /// each named after and referencing that object (non-destructive — nothing is reparented).
    /// On build, CVRMAObjectTogglePass turns each into an AAS toggle + animator layer.
    /// </summary>
    internal static class CVRMACreateToggle
    {
        private const string TogglesContainerName = "Toggles";

        // GameObject menu items are invoked once PER selected object. These flags collapse
        // the burst of per-object calls into a single deferred run (delayCall fires once,
        // after all the per-object invocations for this click have completed).
        private static bool _createPending;
        private static bool _createForSelectionPending;

        // ------------------------------------------------------------------ Create Toggle (empty)

        [MenuItem("GameObject/Modular Avatar CVR/Create Toggle", false, 21)]
        private static void CreateToggle(MenuCommand command)
        {
            if (_createPending) return;
            _createPending = true;

            var active = Selection.activeGameObject;
            EditorApplication.delayCall += () =>
            {
                _createPending = false;
                DoCreateToggle(active);
            };
        }

        private static void DoCreateToggle(GameObject context)
        {
            var avatar = FindAvatar(context);
            if (avatar == null)
            {
                EditorUtility.DisplayDialog("Create Toggle",
                    "Select a GameObject inside an avatar (one with a CVRAvatar component) first.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Toggle");
            int group = Undo.GetCurrentGroup();

            var container = GetOrCreateContainer(avatar.transform);
            var toggle = CreateToggleObject(container, "New Toggle");

            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = toggle.gameObject;
            EditorGUIUtility.PingObject(toggle.gameObject);
        }

        // ------------------------------------------------------------------ Create Toggle for Selection

        [MenuItem("GameObject/Modular Avatar CVR/Create Toggle for Selection", false, 22)]
        private static void CreateToggleForSelection(MenuCommand command)
        {
            if (_createForSelectionPending) return;
            _createForSelectionPending = true;

            // Snapshot the selection NOW — the work runs deferred, after which we mutate
            // the selection, so we must not read Selection.gameObjects inside the deferred call.
            var snapshot = Selection.gameObjects.ToArray();
            EditorApplication.delayCall += () =>
            {
                _createForSelectionPending = false;
                DoCreateToggleForSelection(snapshot);
            };
        }

        private static void DoCreateToggleForSelection(GameObject[] selected)
        {
            if (selected == null || selected.Length == 0) return;

            var avatar = FindAvatar(selected[0]);
            if (avatar == null)
            {
                EditorUtility.DisplayDialog("Create Toggle for Selection",
                    "The selected objects must be inside an avatar (one with a CVRAvatar component).", "OK");
                return;
            }

            // Only objects that belong to this avatar and aren't the avatar itself.
            var targets = selected
                .Where(go => go != null && go != avatar
                             && go.transform.IsChildOf(avatar.transform))
                .Select(go => go.transform)
                .Distinct()
                .ToList();

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Create Toggle for Selection",
                    "None of the selected objects are inside the avatar.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Toggle for Selection");
            int group = Undo.GetCurrentGroup();

            var container = GetOrCreateContainer(avatar.transform);
            CVRMAObjectToggle last = null;

            // One separate toggle component per selected item — each named after its
            // object and referencing the real object (NOT moved).
            foreach (var t in targets)
            {
                var toggle = CreateToggleObject(container, t.name);

                // Default to the object's current visibility so nothing changes until flipped.
                toggle.defaultValue = t.gameObject.activeSelf;
                toggle.objects.Add(new CVRMAToggledObject
                {
                    target = t,
                    activeWhenOn = true
                });

                EditorUtility.SetDirty(toggle);
                last = toggle;
            }

            Undo.CollapseUndoOperations(group);
            if (last != null)
            {
                Selection.activeGameObject = last.gameObject;
                EditorGUIUtility.PingObject(last.gameObject);
            }

            Debug.Log($"[MA-CVR] Created {targets.Count} toggle(s) under '{container.name}'.");
        }

        // ------------------------------------------------------------------ validation

        [MenuItem("GameObject/Modular Avatar CVR/Create Toggle", true)]
        private static bool ValidateCreateToggle() => ValidateInAvatar();

        [MenuItem("GameObject/Modular Avatar CVR/Create Toggle for Selection", true)]
        private static bool ValidateCreateToggleForSelection() => ValidateInAvatar();

        private static bool ValidateInAvatar()
        {
            return Selection.activeGameObject != null
                   && FindAvatar(Selection.activeGameObject) != null
                   && !EditorApplication.isPlaying;
        }

        // ------------------------------------------------------------------ helpers

        private static CVRMAObjectToggle CreateToggleObject(Transform container, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Toggle");
            Undo.SetTransformParent(go.transform, container, "Create Toggle");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            return Undo.AddComponent<CVRMAObjectToggle>(go);
        }

        private static Transform GetOrCreateContainer(Transform avatarRoot)
        {
            // Reuse an existing top-level "Toggles" container (case-insensitive).
            for (int i = 0; i < avatarRoot.childCount; i++)
            {
                var child = avatarRoot.GetChild(i);
                if (child.name.Equals(TogglesContainerName, System.StringComparison.OrdinalIgnoreCase))
                    return child;
            }

            var container = new GameObject(TogglesContainerName);
            Undo.RegisterCreatedObjectUndo(container, "Create Toggles container");
            Undo.SetTransformParent(container.transform, avatarRoot, "Create Toggles container");
            container.transform.localPosition = Vector3.zero;
            container.transform.localRotation = Quaternion.identity;
            container.transform.localScale = Vector3.one;
            return container.transform;
        }

        private static GameObject FindAvatar(GameObject go)
        {
            if (go == null) return null;
            var avatar = go.GetComponentInParent<ABI.CCK.Components.CVRAvatar>(true);
            return avatar != null ? avatar.gameObject : null;
        }
    }
}
#endif
