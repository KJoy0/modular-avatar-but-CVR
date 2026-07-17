#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Drawer for [BlendshapeName(meshField)] string fields.
    /// Replaces the text input with a searchable dropdown listing blendshapes
    /// on the sibling field's SkinnedMeshRenderer. Falls back to a free-text
    /// field when no mesh is assigned, so manual entry still works.
    /// </summary>
    [CustomPropertyDrawer(typeof(BlendshapeNameAttribute))]
    internal class BlendshapeNameDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var attr = (BlendshapeNameAttribute)attribute;
            var smr  = ResolveSibling(property, attr.meshFieldName) as SkinnedMeshRenderer;

            if (smr == null || smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0)
            {
                // No mesh available — fall back to plain text so the field stays editable.
                EditorGUI.BeginChangeCheck();
                var current = property.stringValue;
                var next = EditorGUI.TextField(position, label, current);
                if (EditorGUI.EndChangeCheck())
                    property.stringValue = next;
                return;
            }

            // Build options list. Prefix " — " before each so the picker reads cleanly,
            // and prepend the current value if it isn't found (deleted blendshape).
            var mesh = smr.sharedMesh;
            var names = new List<string>(mesh.blendShapeCount + 1);
            int currentIndex = -1;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                var n = mesh.GetBlendShapeName(i);
                names.Add(n);
                if (n == property.stringValue) currentIndex = i;
            }

            // If current value is missing from mesh, append it as "(missing)" so the user can see it
            string display = property.stringValue;
            if (currentIndex < 0 && !string.IsNullOrEmpty(display))
            {
                names.Add($"{display}  (missing)");
                currentIndex = names.Count - 1;
            }
            else if (currentIndex < 0)
            {
                // empty value — default to first option visually but don't write
                currentIndex = 0;
            }

            // Escape "/" in blendshape names so EditorGUI.Popup doesn't treat them as submenus
            var displayNames = new GUIContent[names.Count];
            for (int i = 0; i < names.Count; i++)
                displayNames[i] = new GUIContent(names[i].Replace("/", "∕"));

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(position, label, currentIndex, displayNames);
            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < mesh.blendShapeCount)
            {
                property.stringValue = mesh.GetBlendShapeName(newIndex);
            }
        }

        /// <summary>
        /// Looks up the sibling field by walking the property path. Works for fields
        /// inside lists/arrays as well as flat siblings on the same object.
        /// </summary>
        private static Object ResolveSibling(SerializedProperty property, string siblingFieldName)
        {
            // Build sibling property path: same parent path, replace the leaf with siblingFieldName
            var path = property.propertyPath;
            int lastDot = path.LastIndexOf('.');
            var siblingPath = lastDot >= 0
                ? path.Substring(0, lastDot + 1) + siblingFieldName
                : siblingFieldName;

            var siblingProp = property.serializedObject.FindProperty(siblingPath);
            if (siblingProp == null || siblingProp.propertyType != SerializedPropertyType.ObjectReference)
                return null;

            return siblingProp.objectReferenceValue;
        }
    }
}
#endif
