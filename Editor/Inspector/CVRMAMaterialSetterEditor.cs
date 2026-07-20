#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAMaterialSetter))]
    internal class CVRMAMaterialSetterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Material Setter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Sets specific material slots on renderers while the parameter is active. " +
                "At build time an AAS Toggle + animator layer is generated.",
                MessageType.None);

            EditorGUILayout.Space(4);
            DrawPropertiesExcluding(serializedObject, "m_Script");

            CVRMAReactivePreview.DrawPreviewToggle((CVRMAMaterialSetter)target);
            CVRMAReactionDebuggerWindow.DrawOpenButton();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
