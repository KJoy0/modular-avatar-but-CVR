#if UNITY_EDITOR
using System.Collections.Generic;
using ABI.CCK.Scripts;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAObjectToggle))]
    internal class CVRMAObjectToggleEditor : UnityEditor.Editor
    {
        private SerializedProperty _label;
        private SerializedProperty _parameter;
        private SerializedProperty _defaultValue;
        private SerializedProperty _objects;

        private void OnEnable()
        {
            _label        = serializedObject.FindProperty("label");
            _parameter    = serializedObject.FindProperty("parameter");
            _defaultValue = serializedObject.FindProperty("defaultValue");
            _objects      = serializedObject.FindProperty("objects");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            CVRMAInspectorUI.DrawHeader(target as Component, "MA Object Toggle");
            EditorGUILayout.HelpBox(
                "Toggles GameObjects — or individual components — when the parameter is active.\n" +
                "Drag objects in from the Hierarchy (multi-select works), or use 'Add Components' " +
                "to toggle just a component's enabled state (Magica Cloth, colliders, audio, particles…).",
                MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_label,        new GUIContent("Label"));
            EditorGUILayout.PropertyField(_parameter,    new GUIContent("Parameter"));
            EditorGUILayout.PropertyField(_defaultValue, new GUIContent("Default (ON)"));
            EditorGUILayout.PropertyField(_objects,      new GUIContent("Toggled Targets"), true);

            // Apply list edits before the drop area mutates the list directly.
            serializedObject.ApplyModifiedProperties();

            DrawDropArea((CVRMAObjectToggle)target);
            DrawAddComponentsButton((CVRMAObjectToggle)target);

            serializedObject.Update();

            CVRMAReactivePreview.DrawPreviewToggle((CVRMAObjectToggle)target);
            CVRMAReactionDebuggerWindow.DrawOpenButton();

            EditorGUILayout.Space(8);

            var toggle = (CVRMAObjectToggle)target;
            var avatarRoot = FindAvatarRoot(toggle.transform);

            using (new EditorGUI.DisabledScope(avatarRoot == null))
            {
                if (GUILayout.Button("Apply to AAS Now", GUILayout.Height(30)))
                    ApplyToAAS(toggle, avatarRoot);
            }

            if (avatarRoot == null)
                EditorGUILayout.HelpBox("No CVRAvatar found in parents.", MessageType.Warning);

            // Status: show if this entry is already in AAS
            if (avatarRoot != null)
            {
                var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
                bool exists = EntryExists(avatar, toggle.GetEffectiveParameter());
                var color = exists ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.85f, 0.3f);
                var prev = GUI.contentColor;
                GUI.contentColor = color;
                EditorGUILayout.LabelField(
                    exists ? "✓ Entry exists in AAS" : "○ Not yet in AAS",
                    EditorStyles.miniLabel);
                GUI.contentColor = prev;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private const int MaxMenuEntries = 250;
        private static GUIStyle _dropStyle;

        /// <summary>
        /// Drop zone accepting a whole Hierarchy multi-selection at once: GameObjects
        /// become active-state entries, Components become enabled-state entries.
        /// </summary>
        private static void DrawDropArea(CVRMAObjectToggle toggle)
        {
            if (_dropStyle == null)
                _dropStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11
                };

            var rect = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
            var evt = Event.current;
            bool hovering = rect.Contains(evt.mousePosition) &&
                            (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform);

            var prev = GUI.color;
            if (hovering && HasDroppableObjects()) GUI.color = new Color(0.6f, 1f, 0.6f);
            GUI.Box(rect, "Drag GameObjects or Components here", _dropStyle);
            GUI.color = prev;

            if (!rect.Contains(evt.mousePosition)) return;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = HasDroppableObjects()
                        ? DragAndDropVisualMode.Link
                        : DragAndDropVisualMode.Rejected;
                    evt.Use();
                    break;

                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    AddDroppedObjects(toggle, DragAndDrop.objectReferences);
                    evt.Use();
                    break;
            }
        }

        private static bool HasDroppableObjects()
        {
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject) return true;
                if (obj is Component c && CVRMAToggledObject.CanToggle(c)) return true;
            }
            return false;
        }

        private static void AddDroppedObjects(CVRMAObjectToggle toggle, UnityEngine.Object[] dropped)
        {
            var existingObjects = new HashSet<Transform>();
            var existingComponents = new HashSet<Component>();
            foreach (var entry in toggle.objects)
            {
                if (entry == null) continue;
                if (entry.TogglesComponent) existingComponents.Add(entry.component);
                else if (entry.target != null) existingObjects.Add(entry.target);
            }

            var added = new List<CVRMAToggledObject>();
            foreach (var obj in dropped)
            {
                switch (obj)
                {
                    case GameObject go when existingObjects.Add(go.transform):
                        added.Add(new CVRMAToggledObject { target = go.transform, activeWhenOn = true });
                        break;

                    case Component c when CVRMAToggledObject.CanToggle(c) && existingComponents.Add(c):
                        added.Add(new CVRMAToggledObject
                        {
                            component = c, target = c.transform, activeWhenOn = true
                        });
                        break;
                }
            }

            if (added.Count == 0) return;

            Undo.RecordObject(toggle, added.Count == 1 ? "Add toggle target" : "Add toggle targets");
            toggle.objects.AddRange(added);
            EditorUtility.SetDirty(toggle);
        }

        /// <summary>
        /// The "automatic" path for component targets: lists every togglable component
        /// under the GameObjects already in the list, so they can be added in one click
        /// instead of being dragged in one at a time.
        /// </summary>
        private static void DrawAddComponentsButton(CVRMAObjectToggle toggle)
        {
            var roots = new List<Transform>();
            foreach (var entry in toggle.objects)
                if (entry != null && !entry.TogglesComponent && entry.target != null)
                    roots.Add(entry.target);

            using (new EditorGUI.DisabledScope(roots.Count == 0))
            {
                if (GUILayout.Button(new GUIContent("Add Components…",
                        "Pick components under the toggled GameObjects to toggle individually.")))
                    ShowComponentMenu(toggle, roots);
            }

            if (roots.Count == 0)
                EditorGUILayout.LabelField(
                    "Add a GameObject target first to pick components from it.", EditorStyles.miniLabel);
        }

        private static void ShowComponentMenu(CVRMAObjectToggle toggle, List<Transform> roots)
        {
            var already = new HashSet<Component>();
            foreach (var entry in toggle.objects)
                if (entry?.component != null) already.Add(entry.component);

            var menu = new GenericMenu();
            var usedLabels = new HashSet<string>();
            int shown = 0;

            foreach (var root in roots)
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || shown >= MaxMenuEntries) continue;
                    if (!CVRMAToggledObject.CanToggle(component)) continue;
                    if (component is CVRMAComponent) continue; // our own build-time markers

                    var relative = RelativePath(root, component.transform);
                    var label = string.IsNullOrEmpty(relative)
                        ? $"{root.name}/{component.GetType().Name}"
                        : $"{root.name}/{relative}/{component.GetType().Name}";

                    // GenericMenu drops duplicate labels — disambiguate same-type siblings.
                    var unique = label;
                    for (int i = 2; !usedLabels.Add(unique); i++) unique = $"{label} ({i})";

                    var captured = component;
                    if (already.Contains(component))
                        menu.AddDisabledItem(new GUIContent(unique), true);
                    else
                        menu.AddItem(new GUIContent(unique), false, () => AddComponentEntry(toggle, captured));
                    shown++;
                }
            }

            if (shown == 0)
                menu.AddDisabledItem(new GUIContent("No togglable components found"));
            else if (shown >= MaxMenuEntries)
                menu.AddDisabledItem(new GUIContent($"… truncated at {MaxMenuEntries} entries"));

            menu.ShowAsContext();
        }

        private static void AddComponentEntry(CVRMAObjectToggle toggle, Component component)
        {
            Undo.RecordObject(toggle, "Add component toggle");
            toggle.objects.Add(new CVRMAToggledObject
            {
                component    = component,
                target       = component.transform,
                activeWhenOn = true
            });
            EditorUtility.SetDirty(toggle);
        }

        private static string RelativePath(Transform root, Transform t)
        {
            if (t == root) return "";
            var parts = new List<string>();
            var cur = t;
            while (cur != null && cur != root) { parts.Insert(0, cur.name); cur = cur.parent; }
            return cur == null ? t.name : string.Join("/", parts);
        }

        private static void ApplyToAAS(CVRMAObjectToggle toggle, GameObject avatarRoot)
        {
            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            if (avatar == null) return;

            Undo.RecordObject(avatar, "Apply Object Toggle to AAS");

            avatar.avatarUsesAdvancedSettings = true;
            if (avatar.avatarSettings == null)
                avatar.avatarSettings = new CVRAdvancedAvatarSettings();
            if (avatar.avatarSettings.settings == null)
                avatar.avatarSettings.settings = new List<CVRAdvancedSettingsEntry>();

            var machineName = toggle.GetEffectiveParameter();

            // Remove existing entry with same machine name first
            avatar.avatarSettings.settings.RemoveAll(e => e.machineName == machineName);

            var entry = CVRMAObjectTogglePass.BuildEntry(toggle);
            if (entry != null)
            {
                avatar.avatarSettings.settings.Add(entry);
                EditorUtility.SetDirty(avatar);
                Debug.Log($"[MA-CVR] ObjectToggle '{toggle.gameObject.name}' applied to AAS as '{machineName}'.");
            }
        }

        private static bool EntryExists(ABI.CCK.Components.CVRAvatar avatar, string machineName)
        {
            if (avatar?.avatarSettings?.settings == null) return false;
            foreach (var s in avatar.avatarSettings.settings)
                if (s.machineName == machineName) return true;
            return false;
        }

        private static GameObject FindAvatarRoot(Transform t)
        {
            while (t != null)
            {
                if (t.GetComponent<ABI.CCK.Components.CVRAvatar>() != null) return t.gameObject;
                t = t.parent;
            }
            return null;
        }
    }

    /// <summary>
    /// One compact row per target: a single field that accepts either a GameObject
    /// (its active state is toggled) or a Component (its enabled state is toggled),
    /// plus the ON-state checkbox.
    /// </summary>
    [CustomPropertyDrawer(typeof(CVRMAToggledObject))]
    internal class CVRMAToggledObjectDrawer : PropertyDrawer
    {
        private const float ToggleWidth = 44f;
        private const float Pad = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var targetProp    = property.FindPropertyRelative("target");
            var componentProp = property.FindPropertyRelative("component");
            var activeProp    = property.FindPropertyRelative("activeWhenOn");

            float line = EditorGUIUtility.singleLineHeight;
            var fieldRect  = new Rect(position.x, position.y, position.width - ToggleWidth - 4f, line);
            var toggleRect = new Rect(position.x + position.width - ToggleWidth, position.y, ToggleWidth, line);

            var component = componentProp.objectReferenceValue as Component;
            var targetTransform = targetProp.objectReferenceValue as Transform;
            UnityEngine.Object current = component != null
                ? component
                : (targetTransform != null ? targetTransform.gameObject : null);

            EditorGUI.BeginChangeCheck();
            var next = EditorGUI.ObjectField(
                fieldRect, GUIContent.none, current, typeof(UnityEngine.Object), true);
            if (EditorGUI.EndChangeCheck())
            {
                switch (next)
                {
                    case GameObject go:
                        targetProp.objectReferenceValue = go.transform;
                        componentProp.objectReferenceValue = null;
                        break;
                    case Component c:
                        componentProp.objectReferenceValue = c;
                        targetProp.objectReferenceValue = c.transform;
                        break;
                    case null:
                        targetProp.objectReferenceValue = null;
                        componentProp.objectReferenceValue = null;
                        break;
                    // Anything else (a material, a texture…) is rejected: keep the old value.
                }
            }

            activeProp.boolValue = EditorGUI.ToggleLeft(toggleRect,
                new GUIContent("ON", "Target is active/enabled while the parameter is ON."),
                activeProp.boolValue);

            if (component != null && !CVRMAToggledObject.CanToggle(component))
            {
                var warnRect = new Rect(position.x, position.y + line + Pad, position.width, line);
                var prev = GUI.contentColor;
                GUI.contentColor = new Color(1f, 0.75f, 0.3f);
                EditorGUI.LabelField(warnRect,
                    $"⚠ {component.GetType().Name} has no enabled state — this entry is skipped.",
                    EditorStyles.miniLabel);
                GUI.contentColor = prev;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            var component = property.FindPropertyRelative("component").objectReferenceValue as Component;
            bool warn = component != null && !CVRMAToggledObject.CanToggle(component);
            return warn ? 2 * line + Pad : line;
        }
    }
}
#endif
