#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAMaterialSwap))]
    internal class CVRMAMaterialSwapEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Material Swap", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Swaps materials (From → To) on every renderer under the swap root while " +
                "the parameter is active. At build time an AAS Toggle + animator layer is generated.",
                MessageType.None);

            EditorGUILayout.Space(4);
            DrawPropertiesExcluding(serializedObject, "m_Script");

            CVRMAReactivePreview.DrawPreviewToggle((CVRMAMaterialSwap)target);
            CVRMAReactionDebuggerWindow.DrawOpenButton();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
