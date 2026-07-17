#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAVisibleHeadAccessory))]
    internal class CVRMAVisibleHeadAccessoryEditor : UnityEditor.Editor
    {
        private SerializedProperty _includeChildren;
        private SerializedProperty _shrinkToZero;

        private void OnEnable()
        {
            _includeChildren = serializedObject.FindProperty("includeChildren");
            _shrinkToZero    = serializedObject.FindProperty("shrinkToZero");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Visible Head Accessory", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Keeps this object visible in first-person view.\n\n" +
                "At build time an FPRExclusion component (isShown = true) is added, " +
                "overriding CVR's TransformHider system which would otherwise shrink or " +
                "cull objects near the head bone.\n\n" +
                "Shrink To Zero: mesh stays loaded (good for physics-driven accessories).\n" +
                "Cut (unchecked): renderer is disabled entirely — lighter but no physics.",
                MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_includeChildren, new GUIContent("Include Children"));
            EditorGUILayout.PropertyField(_shrinkToZero,    new GUIContent("Shrink To Zero"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
