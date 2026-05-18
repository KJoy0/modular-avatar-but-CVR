#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAMenuItem))]
    internal class CVRMAMenuItemEditor : UnityEditor.Editor
    {
        private SerializedProperty _label;
        private SerializedProperty _controlType;
        private SerializedProperty _parameter;
        private SerializedProperty _value;
        private SerializedProperty _defaultValue;
        private SerializedProperty _joystickBaseName;
        private SerializedProperty _isSynced;
        private SerializedProperty _isSaved;

        private void OnEnable()
        {
            _label = serializedObject.FindProperty("label");
            _controlType = serializedObject.FindProperty("controlType");
            _parameter = serializedObject.FindProperty("parameter");
            _value = serializedObject.FindProperty("value");
            _defaultValue = serializedObject.FindProperty("defaultValue");
            _joystickBaseName = serializedObject.FindProperty("joystickBaseName");
            _isSynced = serializedObject.FindProperty("isSynced");
            _isSaved = serializedObject.FindProperty("isSaved");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Menu Item", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.PropertyField(_label, new GUIContent("Label", "Display name in CVR Quick Menu (uses GameObject name if empty)"));
            EditorGUILayout.PropertyField(_controlType, new GUIContent("Control Type"));

            var type = (CVRMAControlType)_controlType.enumValueIndex;

            EditorGUILayout.Space(4);

            switch (type)
            {
                case CVRMAControlType.Toggle:
                case CVRMAControlType.Button:
                    EditorGUILayout.PropertyField(_parameter, new GUIContent("Parameter", "Animator parameter name"));
                    EditorGUILayout.PropertyField(_value, new GUIContent("Value (on)", "Value written when active"));
                    EditorGUILayout.PropertyField(_defaultValue, new GUIContent("Default Value"));
                    DrawCVRMapping("Toggle (Bool/Float/Int)");
                    break;

                case CVRMAControlType.RadialPuppet:
                    EditorGUILayout.PropertyField(_parameter, new GUIContent("Parameter", "Float parameter driven by the radial (0–1)"));
                    EditorGUILayout.PropertyField(_defaultValue, new GUIContent("Default Value"));
                    DrawCVRMapping("Slider");
                    break;

                case CVRMAControlType.TwoAxisPuppet:
                    EditorGUILayout.PropertyField(_joystickBaseName, new GUIContent("Base Name", "CVR creates <BaseName>-x and <BaseName>-y parameters"));
                    DrawCVRMapping("Joystick2D");
                    break;

                case CVRMAControlType.FourAxisPuppet:
                    EditorGUILayout.PropertyField(_joystickBaseName, new GUIContent("Base Name", "CVR maps to Joystick3D; 4th axis is discarded"));
                    EditorGUILayout.HelpBox("FourAxisPuppet maps to CVR Joystick3D (3 axes). The 4th axis is not supported in CVR.", MessageType.Info);
                    DrawCVRMapping("Joystick3D (approx.)");
                    break;

                case CVRMAControlType.SubMenu:
                    EditorGUILayout.HelpBox("SubMenu: child CVRMAMenuItems will be flattened into the AAS list at build time.", MessageType.Info);
                    break;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Sync", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_isSynced, new GUIContent("Network Synced"));
            EditorGUILayout.PropertyField(_isSaved, new GUIContent("Saved Across Changes"));

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawCVRMapping(string cvrType)
        {
            var prev = GUI.color;
            GUI.color = new Color(0.6f, 0.9f, 1f);
            EditorGUILayout.LabelField($"→ CVR AAS: {cvrType}", EditorStyles.miniLabel);
            GUI.color = prev;
        }
    }
}
#endif
