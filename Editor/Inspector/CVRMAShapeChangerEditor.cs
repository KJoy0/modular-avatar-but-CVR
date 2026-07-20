#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAShapeChanger))]
    internal class CVRMAShapeChangerEditor : UnityEditor.Editor
    {
        private SerializedProperty _parameter;
        private SerializedProperty _inverseCondition;
        private SerializedProperty _threshold;
        private SerializedProperty _displacementThreshold;
        private SerializedProperty _defaultValue;
        private SerializedProperty _shapes;

        private void OnEnable()
        {
            _parameter             = serializedObject.FindProperty("parameter");
            _inverseCondition      = serializedObject.FindProperty("inverseCondition");
            _threshold             = serializedObject.FindProperty("threshold");
            _displacementThreshold = serializedObject.FindProperty("displacementThreshold");
            _defaultValue          = serializedObject.FindProperty("defaultValue");
            _shapes                = serializedObject.FindProperty("shapes");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Shape Changer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Set: drives the blendshape to a value while the parameter is active.\n" +
                "Delete: removes the polygons the blendshape displaces while active " +
                "(a collapse blendshape is generated at build so it toggles at runtime).",
                MessageType.None);

            var changer = (CVRMAShapeChanger)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_parameter, new GUIContent("Parameter (optional)",
                "Leave empty to inherit from the nearest parent MA Menu Item, or to become a standalone toggle named after this GameObject."));
            DrawResolvedParameterInfo(changer);
            EditorGUILayout.PropertyField(_defaultValue,          new GUIContent("Default (ON)"));
            EditorGUILayout.PropertyField(_inverseCondition,      new GUIContent("Inverse Condition"));
            EditorGUILayout.PropertyField(_threshold,             new GUIContent("Activation Threshold"));
            EditorGUILayout.PropertyField(_displacementThreshold, new GUIContent("Displacement Threshold"));

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_shapes, new GUIContent("Changed Shapes"), true);

            CVRMAReactivePreview.DrawPreviewToggle(changer);
            CVRMAReactionDebuggerWindow.DrawOpenButton();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Shows where the effective parameter comes from when the field is empty.</summary>
        private static void DrawResolvedParameterInfo(CVRMAShapeChanger changer)
        {
            if (!string.IsNullOrEmpty(changer.parameter)) return;

            var item = changer.GetComponentInParent<CVRMAMenuItem>(true);
            string info = item != null
                ? $"→ inherits '{item.GetEffectiveMachineName()}' from Menu Item '{item.GetEffectiveLabel()}'"
                : $"→ standalone toggle '{changer.GetEffectiveParameter()}' (from GameObject name)";
            EditorGUILayout.LabelField(info, EditorStyles.miniLabel);
        }
    }

    /// <summary>
    /// VRC MA-style row: mesh field with the Set/Delete dropdown beside it, the
    /// blendshape picker underneath, and a value slider only for Set entries.
    /// </summary>
    [CustomPropertyDrawer(typeof(CVRMAChangedShape))]
    internal class CVRMAChangedShapeDrawer : PropertyDrawer
    {
        private const float Pad = 2f;
        private const float TypeWidth = 74f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var meshProp  = property.FindPropertyRelative("targetMesh");
            var shapeProp = property.FindPropertyRelative("shapeName");
            var typeProp  = property.FindPropertyRelative("changeType");
            var valueProp = property.FindPropertyRelative("value");

            float line = EditorGUIUtility.singleLineHeight;

            var meshRect = new Rect(position.x, position.y, position.width - TypeWidth - 4f, line);
            var typeRect = new Rect(position.x + position.width - TypeWidth, position.y, TypeWidth, line);
            EditorGUI.PropertyField(meshRect, meshProp, GUIContent.none);
            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);

            var shapeRect = new Rect(position.x, position.y + line + Pad, position.width, line);
            EditorGUI.PropertyField(shapeRect, shapeProp, GUIContent.none);

            if (typeProp.enumValueIndex == (int)CVRMAShapeChangeType.Set)
            {
                var valueRect = new Rect(position.x, position.y + 2 * (line + Pad), position.width, line);
                EditorGUI.Slider(valueRect, valueProp, 0f, 100f, GUIContent.none);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            bool isSet = property.FindPropertyRelative("changeType").enumValueIndex ==
                         (int)CVRMAShapeChangeType.Set;
            return (isSet ? 3 : 2) * (line + Pad);
        }
    }
}
#endif
