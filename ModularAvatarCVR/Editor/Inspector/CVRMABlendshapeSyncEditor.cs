#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMABlendshapeSync))]
    internal class CVRMABlendshapeSyncEditor : UnityEditor.Editor
    {
        private SerializedProperty _blendshapes;

        private void OnEnable() => _blendshapes = serializedObject.FindProperty("blendshapes");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Blendshape Sync", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Mirrors blendshape values from a source mesh to a target mesh at build time.\n\n" +
                "Each entry maps one blendshape on this object's mesh to a blendshape on another mesh. " +
                "The target value is kept in sync whenever the source changes.",
                MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_blendshapes, new GUIContent("Blendshape Bindings"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
