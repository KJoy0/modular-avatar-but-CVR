#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAMergeBlendTree))]
    internal class CVRMAMergeBlendTreeEditor : UnityEditor.Editor
    {
        private SerializedProperty _motion;
        private SerializedProperty _pathMode;
        private SerializedProperty _layerName;
        private SerializedProperty _layerPriority;

        private void OnEnable()
        {
            _motion        = serializedObject.FindProperty("motion");
            _pathMode      = serializedObject.FindProperty("pathMode");
            _layerName     = serializedObject.FindProperty("layerName");
            _layerPriority = serializedObject.FindProperty("layerPriority");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Merge Blend Tree", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Wraps a BlendTree or AnimationClip into a single-state animator layer " +
                "and merges it into the avatar's animator at build time.\n\n" +
                "Use Path Mode 'Relative' when animation paths are relative to this object, " +
                "or 'Absolute' when they are already relative to the avatar root.",
                MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_motion,        new GUIContent("Motion"));
            EditorGUILayout.PropertyField(_pathMode,      new GUIContent("Path Mode"));
            EditorGUILayout.PropertyField(_layerName,     new GUIContent("Layer Name"));
            EditorGUILayout.PropertyField(_layerPriority, new GUIContent("Layer Priority"));

            if (_motion.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Assign a BlendTree or AnimationClip to Motion.", MessageType.Warning);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
