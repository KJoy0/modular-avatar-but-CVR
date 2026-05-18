#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAMergeArmature))]
    internal class CVRMAMergeArmatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Merge Armature", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Merges this object's armature into the target armature at build time. " +
                "Bone names are matched after stripping the configured prefix/suffix.",
                MessageType.None);

            EditorGUILayout.Space(4);
            DrawDefaultInspector();

            var merger = (CVRMAMergeArmature)target;
            if (merger.mergeTarget == null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("Merge Target is required.", MessageType.Warning);
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Auto-detect Prefix/Suffix"))
            {
                AutoDetect(merger);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void AutoDetect(CVRMAMergeArmature merger)
        {
            if (merger.mergeTarget == null) return;

            // Look at the first child of this transform vs first child of target
            if (merger.transform.childCount == 0 || merger.mergeTarget.childCount == 0) return;

            var myChild = merger.transform.GetChild(0).name;
            var baseChild = merger.mergeTarget.GetChild(0).name;

            int prefixEnd = myChild.IndexOf(baseChild, System.StringComparison.Ordinal);
            if (prefixEnd >= 0)
            {
                Undo.RecordObject(merger, "Auto-detect prefix/suffix");
                merger.prefix = myChild.Substring(0, prefixEnd);
                merger.suffix = myChild.Substring(prefixEnd + baseChild.Length);
                EditorUtility.SetDirty(merger);
            }
            else
            {
                EditorUtility.DisplayDialog("MA-CVR", "Could not auto-detect prefix/suffix from first bone names.", "OK");
            }
        }
    }
}
#endif
