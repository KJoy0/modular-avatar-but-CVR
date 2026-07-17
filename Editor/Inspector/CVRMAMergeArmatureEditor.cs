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
                "Bone names are matched after stripping the prefix/suffix — which is " +
                "auto-detected at build, so you normally don't need to set it manually.",
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
                AutoDetect(merger);

            serializedObject.ApplyModifiedProperties();
        }

        private void AutoDetect(CVRMAMergeArmature merger)
        {
            if (merger.mergeTarget == null) return;

            CVRMAArmatureUtil.DetectPrefixSuffix(merger.transform, merger.mergeTarget,
                out var prefix, out var suffix);

            if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix)
                && !HasDirectMatch(merger))
            {
                EditorUtility.DisplayDialog("MA-CVR",
                    "Could not auto-detect a prefix/suffix. If the outfit's bone names already " +
                    "match the avatar's exactly, no prefix/suffix is needed.", "OK");
                return;
            }

            Undo.RecordObject(merger, "Auto-detect prefix/suffix");
            merger.prefix = prefix;
            merger.suffix = suffix;
            EditorUtility.SetDirty(merger);
        }

        private static bool HasDirectMatch(CVRMAMergeArmature merger)
        {
            foreach (Transform child in merger.transform)
                if (merger.mergeTarget.Find(child.name) != null)
                    return true;
            return false;
        }
    }
}
#endif
