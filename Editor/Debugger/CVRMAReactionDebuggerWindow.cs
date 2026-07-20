#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// CVRMA's answer to VRC MA's Reaction Debugger: shows every reactive component
    /// on the avatar grouped by the parameter that drives it, how that parameter is
    /// bound (menu item / dropdown / auto toggle), what each reaction affects, and
    /// any configuration problems — with Select and Pin-Preview buttons per row.
    /// </summary>
    internal class CVRMAReactionDebuggerWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("Tools/Modular Avatar CVR/Reaction Debugger")]
        internal static void Open() => GetWindow<CVRMAReactionDebuggerWindow>("Reaction Debugger");

        /// <summary>Inspector helper: the "Open Reaction Debugger" button MA users expect.</summary>
        internal static void DrawOpenButton()
        {
            if (GUILayout.Button("Open Reaction Debugger"))
                Open();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += Repaint;
            EditorApplication.hierarchyChanged += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
            EditorApplication.hierarchyChanged -= Repaint;
        }

        private void OnGUI()
        {
            var root = FindAvatarRoot();
            if (root == null)
            {
                EditorGUILayout.HelpBox(
                    "No avatar found. Select an object under a CVRAvatar, or add one to the scene.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Avatar: {root.name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Reactions grouped by driving parameter. Pin previews the ON state in the scene.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            var groups = CollectGroups(root);
            if (groups.Count == 0)
            {
                EditorGUILayout.HelpBox("No reactive components on this avatar.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var group in groups.OrderBy(g => g.Key))
                DrawGroup(root, group.Key, group.Value);
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ data

        private static Transform FindAvatarRoot()
        {
            // Prefer the selection's avatar; fall back to the first avatar in open scenes.
            var selected = Selection.activeGameObject;
            if (selected != null)
            {
                var fromSelection = selected.GetComponentInParent<ABI.CCK.Components.CVRAvatar>(true);
                if (fromSelection != null) return fromSelection.transform;
            }
            var any = Object.FindObjectOfType<ABI.CCK.Components.CVRAvatar>(true);
            return any != null ? any.transform : null;
        }

        private static Dictionary<string, List<Component>> CollectGroups(Transform root)
        {
            var groups = new Dictionary<string, List<Component>>();
            foreach (var c in root.GetComponentsInChildren<CVRMAComponent>(true))
            {
                string parameter = c switch
                {
                    CVRMAShapeChanger sc   => sc.GetEffectiveParameter(),
                    CVRMAObjectToggle ot   => ot.GetEffectiveParameter(),
                    CVRMAMaterialSwap ms   => ms.GetEffectiveParameter(),
                    CVRMAMaterialSetter st => st.GetEffectiveParameter(),
                    _ => null
                };
                if (parameter == null) continue;

                if (!groups.TryGetValue(parameter, out var list))
                    groups[parameter] = list = new List<Component>();
                list.Add(c);
            }
            return groups;
        }

        // ------------------------------------------------------------------ drawing

        private void DrawGroup(Transform root, string parameter, List<Component> components)
        {
            EditorGUILayout.BeginVertical("HelpBox");

            EditorGUILayout.LabelField($"Parameter: {parameter}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(DescribeBinding(root, parameter), EditorStyles.miniLabel);

            foreach (var c in components)
            {
                EditorGUILayout.BeginHorizontal();

                bool previewing = CVRMAReactivePreview.IsPreviewing(c);
                var prev = GUI.contentColor;
                if (previewing) GUI.contentColor = new Color(0.4f, 1f, 0.4f);
                EditorGUILayout.LabelField(
                    $"{(previewing ? "▶ " : "")}{TypeLabel(c)}  —  {RelativePath(root, c.transform)}",
                    GUILayout.MinWidth(180));
                GUI.contentColor = prev;

                EditorGUILayout.LabelField(Summarize(c), EditorStyles.miniLabel, GUILayout.MinWidth(120));

                if (GUILayout.Button("Select", GUILayout.Width(52)))
                    Selection.activeGameObject = c.gameObject;

                bool pinned = CVRMAReactivePreview.IsPinned(c);
                bool newPinned = GUILayout.Toggle(pinned, pinned ? "Unpin" : "Pin", "Button", GUILayout.Width(52));
                if (newPinned != pinned) CVRMAReactivePreview.SetPinned(c, newPinned);

                EditorGUILayout.EndHorizontal();

                foreach (var warning in Validate(c))
                {
                    var color = GUI.contentColor;
                    GUI.contentColor = new Color(1f, 0.75f, 0.3f);
                    EditorGUILayout.LabelField($"    ⚠ {warning}", EditorStyles.miniLabel);
                    GUI.contentColor = color;
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private static string DescribeBinding(Transform root, string parameter)
        {
            var drivers = new List<CVRMAMenuItem>();
            foreach (var item in root.GetComponentsInChildren<CVRMAMenuItem>(true))
            {
                if (item.controlType != CVRMAControlType.Toggle &&
                    item.controlType != CVRMAControlType.Button) continue;
                if (item.GetEffectiveMachineName() == parameter)
                    drivers.Add(item);
            }

            if (drivers.Count == 0)
                return "Bool toggle — AAS entry auto-generated by the reactive pass.";

            var first = drivers[0];
            switch (first.parameterType)
            {
                case CVRMAMenuParameterType.Int:
                    var options = string.Join(", ", drivers
                        .OrderBy(d => d.value)
                        .Select(d => $"'{d.GetEffectiveLabel()}' = {Mathf.RoundToInt(d.value)}"));
                    return $"Int dropdown via Menu Item(s): {options}";
                case CVRMAMenuParameterType.Float:
                    return $"Float toggle via Menu Item '{first.GetEffectiveLabel()}'";
                default:
                    return $"Bool toggle via Menu Item '{first.GetEffectiveLabel()}'";
            }
        }

        private static string TypeLabel(Component c) => c switch
        {
            CVRMAShapeChanger _   => "Shape Changer",
            CVRMAObjectToggle _   => "Object Toggle",
            CVRMAMaterialSwap _   => "Material Swap",
            CVRMAMaterialSetter _ => "Material Setter",
            _ => c.GetType().Name
        };

        private static string Summarize(Component c)
        {
            switch (c)
            {
                case CVRMAShapeChanger sc:
                    int set = sc.shapes?.Count(s => s.changeType == CVRMAShapeChangeType.Set) ?? 0;
                    int del = sc.shapes?.Count(s => s.changeType == CVRMAShapeChangeType.Delete) ?? 0;
                    return $"{set} set, {del} delete{(sc.inverseCondition ? "  (inverse)" : "")}";
                case CVRMAObjectToggle ot:
                    return $"{ot.objects?.Count ?? 0} object(s)";
                case CVRMAMaterialSwap ms:
                    return $"{ms.swaps?.Count ?? 0} swap(s)";
                case CVRMAMaterialSetter st:
                    return $"{st.entries?.Count ?? 0} slot(s)";
                default:
                    return "";
            }
        }

        private static IEnumerable<string> Validate(Component c)
        {
            switch (c)
            {
                case CVRMAShapeChanger sc:
                    if (sc.shapes == null || sc.shapes.Count == 0)
                        yield return "no shapes configured";
                    else
                        foreach (var s in sc.shapes)
                        {
                            if (s.targetMesh == null) { yield return "shape entry has no target mesh"; continue; }
                            if (string.IsNullOrEmpty(s.shapeName)) { yield return $"'{s.targetMesh.name}': no blendshape selected"; continue; }
                            if (s.targetMesh.sharedMesh != null &&
                                s.targetMesh.sharedMesh.GetBlendShapeIndex(s.shapeName) < 0)
                                yield return $"'{s.shapeName}' not found on '{s.targetMesh.name}'";
                        }
                    break;

                case CVRMAObjectToggle ot:
                    if (ot.objects == null || ot.objects.Count == 0)
                        yield return "no objects configured";
                    else if (ot.objects.Any(o => o?.target == null))
                        yield return "entry with missing target object";
                    break;

                case CVRMAMaterialSwap ms:
                    if (ms.swaps == null || ms.swaps.Count == 0)
                        yield return "no swaps configured";
                    else if (ms.swaps.Any(s => s?.from == null || s.to == null))
                        yield return "swap entry with missing From/To material";
                    break;

                case CVRMAMaterialSetter st:
                    if (st.entries == null || st.entries.Count == 0)
                        yield return "no entries configured";
                    else
                        foreach (var e in st.entries)
                        {
                            if (e?.targetRenderer == null) { yield return "entry with missing renderer"; continue; }
                            if (e.material == null) { yield return $"'{e.targetRenderer.name}': no material assigned"; continue; }
                            if (e.materialIndex < 0 || e.materialIndex >= e.targetRenderer.sharedMaterials.Length)
                                yield return $"'{e.targetRenderer.name}': slot {e.materialIndex} out of range";
                        }
                    break;
            }
        }

        private static string RelativePath(Transform root, Transform t)
        {
            if (t == root) return root.name;
            var parts = new List<string>();
            var current = t;
            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return string.Join("/", parts);
        }
    }
}
#endif
