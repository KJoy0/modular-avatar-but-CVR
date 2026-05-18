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

            EditorGUILayout.LabelField("MA Object Toggle", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Toggles GameObjects when the parameter is active.\n" +
                "Click 'Apply to AAS' to write the entry to the avatar's Advanced Settings " +
                "immediately (without building), so you can preview it in the CCK inspector.",
                MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_label,        new GUIContent("Label"));
            EditorGUILayout.PropertyField(_parameter,    new GUIContent("Parameter"));
            EditorGUILayout.PropertyField(_defaultValue, new GUIContent("Default (ON)"));
            EditorGUILayout.PropertyField(_objects,      new GUIContent("Toggled Objects"), true);

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
}
#endif
