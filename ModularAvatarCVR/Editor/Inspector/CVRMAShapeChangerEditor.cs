#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAShapeChanger))]
    internal class CVRMAShapeChangerEditor : UnityEditor.Editor
    {
        private SerializedProperty _parameter;
        private SerializedProperty _threshold;
        private SerializedProperty _defaultValue;
        private SerializedProperty _shapes;

        private void OnEnable()
        {
            _parameter    = serializedObject.FindProperty("parameter");
            _threshold    = serializedObject.FindProperty("threshold");
            _defaultValue = serializedObject.FindProperty("defaultValue");
            _shapes       = serializedObject.FindProperty("shapes");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Shape Changer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Sets or deletes blendshapes on meshes when a Bool parameter is active.\n\n" +
                "At build time an AAS Toggle entry is created with ON/OFF animation clips " +
                "that drive the listed blendshape values.",
                MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_parameter,    new GUIContent("Parameter"));
            EditorGUILayout.PropertyField(_threshold,    new GUIContent("Threshold"));
            EditorGUILayout.PropertyField(_defaultValue, new GUIContent("Default (ON)"));
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_shapes, new GUIContent("Changed Shapes"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
