#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMABoneProxy))]
    internal class CVRMABoneProxyEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Bone Proxy", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Reparents this object to the target bone at build time. " +
                "In editor, mirrors the target's transform for preview.",
                MessageType.None);

            EditorGUILayout.Space(4);
            DrawDefaultInspector();

            var proxy = (CVRMABoneProxy)target;
            if (proxy.target == null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("Target bone is required.", MessageType.Warning);
            }
            else if (!string.IsNullOrEmpty(proxy.subPath))
            {
                var resolved = proxy.target.Find(proxy.subPath);
                if (resolved == null)
                    EditorGUILayout.HelpBox($"Sub-path '{proxy.subPath}' not found under target.", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
