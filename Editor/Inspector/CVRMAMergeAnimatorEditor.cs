#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAMergeAnimator))]
    internal class CVRMAMergeAnimatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Merge Animator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Merges an AnimatorController into the avatar's override controller at build time.\n" +
                "Relative path mode re-prefixes all animation bindings to this object's position in the hierarchy.",
                MessageType.None);

            EditorGUILayout.Space(4);
            DrawDefaultInspector();

            var merger = (CVRMAMergeAnimator)target;
            if (merger.animator == null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("Animator controller is required.", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
