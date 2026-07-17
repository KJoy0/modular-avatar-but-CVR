#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAMeshCutter))]
    internal class CVRMAMeshCutterEditor : UnityEditor.Editor
    {
        private const int MaxPreviewPoints = 4000;

        private SerializedProperty _targetMesh;
        private SerializedProperty _mode;
        private SerializedProperty _action;
        private SerializedProperty _filters;
        private SerializedProperty _label;
        private SerializedProperty _parameter;
        private SerializedProperty _defaultValue;
        private SerializedProperty _hiddenWhenOn;

        // Selection preview cache (world-space positions of cut vertices).
        private Vector3[] _previewPoints;
        private string _previewSummary;

        private void OnEnable()
        {
            _targetMesh   = serializedObject.FindProperty("targetMesh");
            _mode         = serializedObject.FindProperty("mode");
            _action       = serializedObject.FindProperty("action");
            _filters      = serializedObject.FindProperty("filters");
            _label        = serializedObject.FindProperty("label");
            _parameter    = serializedObject.FindProperty("parameter");
            _defaultValue = serializedObject.FindProperty("defaultValue");
            _hiddenWhenOn = serializedObject.FindProperty("hiddenWhenOn");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Mesh Cutter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Cuts the portion of a mesh selected by the vertex filters at build time.\n\n" +
                "Delete removes it permanently (on a cloned mesh — the original asset is safe). " +
                "Toggle splits it into a child renderer driven by a CVR AAS toggle.",
                MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_targetMesh, new GUIContent("Target Mesh"));
            EditorGUILayout.PropertyField(_mode,       new GUIContent("Filter Combine"));
            EditorGUILayout.PropertyField(_action,     new GUIContent("Action"));

            if (_action.enumValueIndex == (int)CVRMAMeshCutterAction.Toggle)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Toggle", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_label,        new GUIContent("Menu Label"));
                EditorGUILayout.PropertyField(_parameter,    new GUIContent("Parameter"));
                EditorGUILayout.PropertyField(_defaultValue, new GUIContent("Default (ON)"));
                EditorGUILayout.PropertyField(_hiddenWhenOn, new GUIContent("Hidden When ON"));
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_filters, new GUIContent("Vertex Filters"), true);

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Preview Selection"))
                RecomputePreview();
            if (GUILayout.Button("Clear Preview") && _previewPoints != null)
            {
                _previewPoints = null;
                _previewSummary = null;
                SceneView.RepaintAll();
            }
            if (!string.IsNullOrEmpty(_previewSummary))
                EditorGUILayout.HelpBox(_previewSummary, MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void RecomputePreview()
        {
            _previewPoints = null;
            _previewSummary = null;

            var cutter = (CVRMAMeshCutter)target;
            var smr = cutter.GetEffectiveTarget();
            if (smr == null || smr.sharedMesh == null)
            {
                _previewSummary = "No target mesh assigned.";
                return;
            }

            if (!CVRMAMeshCutterUtil.ComputeCut(cutter, out _, out _, out int cutTris, out var cutVertices))
            {
                _previewSummary = "Selection could not be computed — see Console for details.";
                return;
            }

            var mesh = smr.sharedMesh;
            int selectedCount = 0;
            foreach (var s in cutVertices) if (s) selectedCount++;

            // Cache world positions for the scene overlay (sampled if very dense).
            var vertices = mesh.vertices;
            var toWorld = smr.transform.localToWorldMatrix;
            var points = new List<Vector3>(Mathf.Min(selectedCount, MaxPreviewPoints));
            int step = Mathf.Max(1, selectedCount / MaxPreviewPoints);
            int seen = 0;
            for (int v = 0; v < vertices.Length; v++)
            {
                if (!cutVertices[v]) continue;
                if (seen++ % step == 0)
                    points.Add(toWorld.MultiplyPoint3x4(vertices[v]));
            }
            _previewPoints = points.ToArray();

            _previewSummary =
                $"{cutTris:N0} triangle(s) will be cut, touching {selectedCount:N0} / {mesh.vertexCount:N0} vertices.\n" +
                "Note: the preview shows rest-pose vertex positions (skinning is not applied).";
            SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            if (_previewPoints == null || _previewPoints.Length == 0) return;

            Handles.color = new Color(1f, 0.25f, 0.25f, 0.9f);
            foreach (var p in _previewPoints)
            {
                float size = HandleUtility.GetHandleSize(p) * 0.01f;
                Handles.DotHandleCap(0, p, Quaternion.identity, size, EventType.Repaint);
            }
        }
    }

    /// <summary>
    /// Draws a CVRMAVertexFilter showing only the fields relevant to its type,
    /// with a blendshape picker sourced from the owning cutter's target mesh.
    /// </summary>
    [CustomPropertyDrawer(typeof(CVRMAVertexFilter))]
    internal class CVRMAVertexFilterDrawer : PropertyDrawer
    {
        private const float Pad = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, FilterLabel(property), true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            line = NextLine(line, line.height);
            EditorGUI.PropertyField(line, property.FindPropertyRelative("type"));
            line = NextLine(line, line.height);
            EditorGUI.PropertyField(line, property.FindPropertyRelative("invert"));
            line = NextLine(line, line.height);
            EditorGUI.PropertyField(line, property.FindPropertyRelative("selectionMode"),
                new GUIContent("Triangle Mode"));

            switch (FilterType(property))
            {
                case CVRMAVertexFilterType.ByBone:
                    line = NextLine(line, line.height);
                    EditorGUI.PropertyField(line, property.FindPropertyRelative("bone"));
                    line = NextLine(line, line.height);
                    EditorGUI.PropertyField(line, property.FindPropertyRelative("includeChildBones"));
                    line = NextLine(line, line.height);
                    EditorGUI.PropertyField(line, property.FindPropertyRelative("minBoneWeight"));
                    break;

                case CVRMAVertexFilterType.ByBlendshape:
                    var namesProp = property.FindPropertyRelative("blendshapeNames");
                    float namesHeight = EditorGUI.GetPropertyHeight(namesProp, true);
                    line = NextLine(line, line.height);
                    line.height = namesHeight;
                    EditorGUI.PropertyField(line, namesProp, new GUIContent("Blendshapes"), true);
                    line = NextLine(line, namesHeight);
                    EditorGUI.PropertyField(line, property.FindPropertyRelative("minShapeDelta"));
                    break;

                case CVRMAVertexFilterType.ByAxis:
                    line = NextLine(line, line.height);
                    EditorGUI.PropertyField(line, property.FindPropertyRelative("planeOrigin"));
                    line = NextLine(line, line.height);
                    EditorGUI.PropertyField(line, property.FindPropertyRelative("planeNormal"));
                    break;

                case CVRMAVertexFilterType.ByMask:
                    line = NextLine(line, line.height);
                    EditorGUI.PropertyField(line, property.FindPropertyRelative("maskTexture"));
                    line = NextLine(line, line.height);
                    EditorGUI.PropertyField(line, property.FindPropertyRelative("maskMode"));
                    line = NextLine(line, line.height);
                    EditorGUI.PropertyField(line, property.FindPropertyRelative("materialSlot"));
                    line = NextLine(line, line.height);
                    EditorGUI.PropertyField(line, property.FindPropertyRelative("uvChannel"));
                    break;

                case CVRMAVertexFilterType.ByUVTile:
                    line = NextLine(line, line.height);
                    EditorGUI.PropertyField(line, property.FindPropertyRelative("uvChannel"));
                    line = NextLine(line, line.height);
                    EditorGUI.PropertyField(line, property.FindPropertyRelative("tileX"));
                    line = NextLine(line, line.height);
                    EditorGUI.PropertyField(line, property.FindPropertyRelative("tileY"));
                    break;
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return h;

            // header + type + invert + selectionMode
            float total = 4 * (h + Pad);

            switch (FilterType(property))
            {
                case CVRMAVertexFilterType.ByBone:
                    total += 3 * (h + Pad);
                    break;
                case CVRMAVertexFilterType.ByBlendshape:
                    total += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("blendshapeNames"), true) + Pad;
                    total += h + Pad;
                    break;
                case CVRMAVertexFilterType.ByAxis:
                    total += 2 * (h + Pad);
                    break;
                case CVRMAVertexFilterType.ByMask:
                    total += 4 * (h + Pad);
                    break;
                case CVRMAVertexFilterType.ByUVTile:
                    total += 3 * (h + Pad);
                    break;
            }
            return total;
        }

        /// <summary>Advances below the previous rect (whose height was <paramref name="previousHeight"/>), one line tall by default.</summary>
        private static Rect NextLine(Rect current, float previousHeight) =>
            new Rect(current.x, current.y + previousHeight + Pad,
                     current.width, EditorGUIUtility.singleLineHeight);

        private static CVRMAVertexFilterType FilterType(SerializedProperty property) =>
            (CVRMAVertexFilterType)property.FindPropertyRelative("type").enumValueIndex;

        private static GUIContent FilterLabel(SerializedProperty property)
        {
            var type = FilterType(property);
            bool invert = property.FindPropertyRelative("invert").boolValue;
            return new GUIContent(invert ? $"{type} (inverted)" : type.ToString());
        }
    }
}
#endif
