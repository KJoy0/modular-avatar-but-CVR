#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAFloorAdjuster))]
    internal class CVRMAFloorAdjusterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("MA Floor Adjuster", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Position this GameObject vertically at the bottom of your shoes " +
                "(a side-on orthographic view helps).\n\n" +
                "At build time the avatar is raised so this height becomes the floor — " +
                "view and voice positions follow automatically.\n\n" +
                "Only one Floor Adjuster may be active per avatar; with more than one, " +
                "no adjustment is applied.",
                MessageType.None);

            var adjuster = (CVRMAFloorAdjuster)target;
            var root = adjuster.transform.root;
            float delta = root.position.y - adjuster.transform.position.y;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Current adjustment",
                delta >= 0 ? $"raise avatar by {delta:F4}m" : $"lower avatar by {-delta:F4}m");
        }
    }
}
#endif
