#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAParameters))]
    internal class CVRMAParametersEditor : UnityEditor.Editor
    {
        private SerializedProperty _parameters;

        private void OnEnable() => _parameters = serializedObject.FindProperty("parameters");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Parameters", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Declares animator parameters and maps them to CVR Advanced Avatar Settings.\n\n" +
                "• Bool → AAS Toggle\n" +
                "• Float → AAS Slider\n" +
                "• Int → AAS Dropdown\n" +
                "• NotSynced → parameter only, no AAS entry",
                MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_parameters, new GUIContent("Parameters"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
