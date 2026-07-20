#if UNITY_EDITOR
using System.Collections.Generic;
using ABI.CCK.Scripts;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Converts each CVRMAShapeChanger following VRC MA's reaction model. The
    /// condition driving the shapes is resolved in order:
    ///
    ///   1. explicit parameter          → own toggle (animator layer + AAS entry)
    ///   2. parent MA Menu Item         → that item's parameter (typed binding)
    ///   3. an Object Toggle targeting this object or an ancestor
    ///                                  → follows that toggle's parameter (no own entry)
    ///   4. nothing drives it, object active   → baked statically: Set applied to the
    ///                                    mesh, Delete polygons permanently removed
    ///   5. nothing drives it, object inactive → never active; component removed
    ///
    /// Set entries animate the blendshape to the given value while active.
    /// Delete entries remove the polygons DISPLACED by the blendshape (beyond the
    /// displacement threshold). Animated deletion uses a generated collapse
    /// blendshape on a cloned mesh (meshes can't be re-cut at runtime).
    ///
    /// See CVRMAAnimatorUtil for why AAS useAnimationClip can't be used.
    /// </summary>
    internal static class CVRMAShapeChangerPass
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";

        private enum ConditionKind { Animated, Static, Never }

        private struct Condition
        {
            public ConditionKind kind;
            public string machineName;
            public bool inverse;
            public bool registerAAS;    // false when another component owns the AAS entry
            public GameObject bindingHost; // where to resolve the menu Int/Float binding from
        }

        internal static void Run(GameObject avatarRoot)
        {
            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            if (avatar == null) return;

            var changers = avatarRoot.GetComponentsInChildren<CVRMAShapeChanger>(true);
            if (changers.Length == 0) return;

            avatar.avatarUsesAdvancedSettings = true;
            if (avatar.avatarSettings == null)
                avatar.avatarSettings = new CVRAdvancedAvatarSettings();
            if (avatar.avatarSettings.settings == null)
                avatar.avatarSettings.settings = new List<CVRAdvancedSettingsEntry>();

            foreach (var changer in changers)
            {
                if (changer.shapes == null || changer.shapes.Count == 0)
                {
                    Object.DestroyImmediate(changer);
                    continue;
                }

                var condition = ResolveCondition(avatarRoot, changer);
                switch (condition.kind)
                {
                    case ConditionKind.Never:
                        Debug.Log(
                            $"[MA-CVR] ShapeChanger '{changer.gameObject.name}': object is disabled and " +
                            "nothing toggles it — reaction can never fire, removed.");
                        break;

                    case ConditionKind.Static:
                        BakeStatic(changer);
                        break;

                    default:
                        BuildAnimated(avatarRoot, avatar, changer, condition);
                        break;
                }

                Object.DestroyImmediate(changer);
            }
        }

        // ------------------------------------------------------------------ condition

        private static Condition ResolveCondition(GameObject avatarRoot, CVRMAShapeChanger changer)
        {
            if (!string.IsNullOrEmpty(changer.parameter))
                return new Condition
                {
                    kind = ConditionKind.Animated,
                    machineName = changer.parameter,
                    inverse = changer.inverseCondition,
                    registerAAS = true,
                    bindingHost = changer.gameObject
                };

            var item = changer.GetComponentInParent<CVRMAMenuItem>(true);
            if (item != null)
                return new Condition
                {
                    kind = ConditionKind.Animated,
                    machineName = item.GetEffectiveMachineName(),
                    inverse = changer.inverseCondition,
                    registerAAS = true,
                    bindingHost = changer.gameObject
                };

            var (toggle, activeWhenOn) = FindDrivingToggle(avatarRoot, changer.transform);
            if (toggle != null)
                return new Condition
                {
                    kind = ConditionKind.Animated,
                    machineName = toggle.GetEffectiveParameter(),
                    // If the toggle HIDES the object when ON, the reaction runs when OFF.
                    inverse = changer.inverseCondition ^ !activeWhenOn,
                    registerAAS = false, // the ObjectToggle pass owns the AAS entry
                    bindingHost = toggle.gameObject
                };

            bool staticallyOn = changer.gameObject.activeInHierarchy != changer.inverseCondition;
            return new Condition { kind = staticallyOn ? ConditionKind.Static : ConditionKind.Never };
        }

        /// <summary>Finds an Object Toggle whose entries drive this object or an ancestor of it.</summary>
        private static (CVRMAObjectToggle toggle, bool activeWhenOn) FindDrivingToggle(
            GameObject avatarRoot, Transform t)
        {
            foreach (var toggle in avatarRoot.GetComponentsInChildren<CVRMAObjectToggle>(true))
            {
                if (toggle.objects == null) continue;
                foreach (var entry in toggle.objects)
                {
                    if (entry?.target == null) continue;
                    if (t == entry.target || t.IsChildOf(entry.target))
                        return (toggle, entry.activeWhenOn);
                }
            }
            return (null, true);
        }

        /// <summary>
        /// Human-readable condition source for the inspector and Reaction Debugger.
        /// <paramref name="machineName"/> is null when the reaction is static/never.
        /// </summary>
        internal static string DescribeConditionSource(CVRMAShapeChanger changer, out string machineName)
        {
            machineName = null;

            if (!string.IsNullOrEmpty(changer.parameter))
            {
                machineName = changer.parameter;
                return $"explicit parameter '{machineName}'";
            }

            var item = changer.GetComponentInParent<CVRMAMenuItem>(true);
            if (item != null)
            {
                machineName = item.GetEffectiveMachineName();
                return $"inherits '{machineName}' from Menu Item '{item.GetEffectiveLabel()}'";
            }

            var avatar = changer.GetComponentInParent<ABI.CCK.Components.CVRAvatar>(true);
            var root = avatar != null ? avatar.gameObject : changer.transform.root.gameObject;
            var (toggle, activeWhenOn) = FindDrivingToggle(root, changer.transform);
            if (toggle != null)
            {
                machineName = toggle.GetEffectiveParameter();
                return $"follows Object Toggle '{toggle.gameObject.name}' ('{machineName}'" +
                       (activeWhenOn ? ")" : ", inverted)");
            }

            bool staticallyOn = changer.gameObject.activeInHierarchy != changer.inverseCondition;
            return staticallyOn
                ? "applies statically at build (object always active)"
                : "never active (object disabled, nothing toggles it)";
        }

        // ------------------------------------------------------------------ static bake

        private static void BakeStatic(CVRMAShapeChanger changer)
        {
            int setCount = 0;

            foreach (var shape in changer.shapes)
            {
                if (shape.changeType != CVRMAShapeChangeType.Set) continue;
                if (shape.targetMesh == null || shape.targetMesh.sharedMesh == null) continue;
                if (string.IsNullOrEmpty(shape.shapeName)) continue;

                int idx = shape.targetMesh.sharedMesh.GetBlendShapeIndex(shape.shapeName);
                if (idx < 0)
                {
                    Debug.LogWarning(
                        $"[MA-CVR] ShapeChanger '{changer.gameObject.name}': blendshape " +
                        $"'{shape.shapeName}' not found on '{shape.targetMesh.name}' — skipped.");
                    continue;
                }
                shape.targetMesh.SetBlendShapeWeight(idx, shape.value);
                setCount++;
            }

            int deletedTris = 0;
            foreach (var kv in GroupDeleteEntries(changer))
            {
                var smr = kv.Key;
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;

                var matched = ComputeDisplacedVertices(
                    mesh, kv.Value, changer.displacementThreshold, changer.gameObject.name);
                if (matched == null) continue;

                var kept = KeepTrianglesNotFullyMatched(mesh, matched, out int removed);
                if (removed == 0) continue;

                var clone = CVRMAMeshCutterUtil.BuildCompactedMesh(mesh, kept, mesh.name + "_ShapeDel");
                EnsureTempFolder();
                AssetDatabase.CreateAsset(clone, $"{TempFolder}/MA_CVR_{clone.name}_{GUID.Generate()}.asset");
                smr.sharedMesh = clone;
                deletedTris += removed;
            }

            Debug.Log(
                $"[MA-CVR] ShapeChanger '{changer.gameObject.name}': baked statically " +
                $"({setCount} shape(s) set, {deletedTris} tri(s) deleted).");
        }

        // ------------------------------------------------------------------ animated

        private static void BuildAnimated(
            GameObject avatarRoot, ABI.CCK.Components.CVRAvatar avatar,
            CVRMAShapeChanger changer, Condition condition)
        {
            var machineName = condition.machineName;

            // Delete entries first: clone meshes and generate collapse blendshapes.
            var deleteShapes = PrepareDeleteShapes(changer, machineName);

            var activeClip   = BuildClip(avatarRoot, changer, deleteShapes, active: true,  name: $"{machineName}_ShapeOn");
            var inactiveClip = BuildClip(avatarRoot, changer, deleteShapes, active: false, name: $"{machineName}_ShapeOff");

            var onClip  = condition.inverse ? inactiveClip : activeClip;
            var offClip = condition.inverse ? activeClip   : inactiveClip;

            // Build a real animator layer and merge it (the AAS clips path is a dead end).
            var (paramType, compareValue, menuOwned) =
                CVRMAAnimatorUtil.ResolveMenuBinding(condition.bindingHost, machineName);
            var controller = CVRMAAnimatorUtil.BuildToggleController(
                machineName, onClip, offClip, changer.defaultValue, paramType, compareValue);
            if (controller != null)
                CVRMAAnimatorUtil.InjectMergeAnimator(changer.gameObject, controller);

            // Register the menu parameter unless another component owns the entry
            // (an Int/Float menu item, or the Object Toggle this reaction follows).
            if (condition.registerAAS && !menuOwned)
                CVRMAAASUtil.AddOrMergeToggleEntry(avatar,
                    CVRMAAnimatorUtil.BuildMenuEntry(changer.gameObject.name, machineName, changer.defaultValue));
        }

        // ------------------------------------------------------------------ delete helpers

        private static Dictionary<SkinnedMeshRenderer, List<string>> GroupDeleteEntries(CVRMAShapeChanger changer)
        {
            var byMesh = new Dictionary<SkinnedMeshRenderer, List<string>>();
            foreach (var shape in changer.shapes)
            {
                if (shape.changeType != CVRMAShapeChangeType.Delete) continue;
                if (shape.targetMesh == null || string.IsNullOrEmpty(shape.shapeName)) continue;
                if (!byMesh.TryGetValue(shape.targetMesh, out var list))
                    byMesh[shape.targetMesh] = list = new List<string>();
                list.Add(shape.shapeName);
            }
            return byMesh;
        }

        /// <summary>Per-submesh triangle lists excluding triangles whose vertices are all matched.</summary>
        internal static List<int>[] KeepTrianglesNotFullyMatched(Mesh mesh, bool[] matched, out int removedTris)
        {
            var kept = new List<int>[mesh.subMeshCount];
            removedTris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                kept[s] = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    if (matched[tris[t]] && matched[tris[t + 1]] && matched[tris[t + 2]])
                    {
                        removedTris++;
                        continue;
                    }
                    kept[s].Add(tris[t]); kept[s].Add(tris[t + 1]); kept[s].Add(tris[t + 2]);
                }
            }
            return kept;
        }

        /// <summary>
        /// For every mesh with Delete entries: clones it, adds a blendshape that
        /// collapses all vertices displaced (≥ displacementThreshold) by any of the
        /// listed shapes, and swaps the clone onto the renderer. Returns the
        /// (renderer, generated shape name) pairs to animate.
        /// </summary>
        private static List<(SkinnedMeshRenderer smr, string shapeName)> PrepareDeleteShapes(
            CVRMAShapeChanger changer, string machineName)
        {
            var results = new List<(SkinnedMeshRenderer, string)>();

            foreach (var kv in GroupDeleteEntries(changer))
            {
                var smr = kv.Key;
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;

                var matched = ComputeDisplacedVertices(
                    mesh, kv.Value, changer.displacementThreshold, changer.gameObject.name);
                if (matched == null) continue;

                var clone = Object.Instantiate(mesh);
                clone.name = mesh.name + "_Del";

                var genShape = $"MA_Del_{machineName}";
                while (clone.GetBlendShapeIndex(genShape) >= 0)
                    genShape += "_";

                clone.AddBlendShapeFrame(genShape, 100f,
                    BuildCollapseDeltas(mesh, matched),
                    new Vector3[mesh.vertexCount], new Vector3[mesh.vertexCount]);

                EnsureTempFolder();
                AssetDatabase.CreateAsset(clone, $"{TempFolder}/MA_CVR_{clone.name}_{GUID.Generate()}.asset");
                smr.sharedMesh = clone;

                results.Add((smr, genShape));
            }
            return results;
        }

        /// <summary>Flags vertices moved ≥ threshold by ANY of the named blendshapes.</summary>
        internal static bool[] ComputeDisplacedVertices(
            Mesh mesh, List<string> shapeNames, float threshold, string owner)
        {
            var matched = new bool[mesh.vertexCount];
            var dv = new Vector3[mesh.vertexCount];
            var dn = new Vector3[mesh.vertexCount];
            var dt = new Vector3[mesh.vertexCount];
            float minSqr = threshold * threshold;
            bool any = false;

            foreach (var name in shapeNames)
            {
                int idx = mesh.GetBlendShapeIndex(name);
                if (idx < 0)
                {
                    Debug.LogWarning(
                        $"[MA-CVR] ShapeChanger '{owner}': blendshape '{name}' not found on '{mesh.name}' — skipped.");
                    continue;
                }
                mesh.GetBlendShapeFrameVertices(idx, mesh.GetBlendShapeFrameCount(idx) - 1, dv, dn, dt);
                for (int v = 0; v < dv.Length; v++)
                {
                    if (dv[v].sqrMagnitude < minSqr) continue;
                    matched[v] = true;
                    any = true;
                }
            }

            if (!any)
            {
                Debug.LogWarning(
                    $"[MA-CVR] ShapeChanger '{owner}': Delete shapes displaced no vertices on '{mesh.name}' " +
                    "(check the displacement threshold).");
                return null;
            }
            return matched;
        }

        /// <summary>Deltas moving every matched vertex to the matched region's centroid (degenerating its triangles).</summary>
        private static Vector3[] BuildCollapseDeltas(Mesh mesh, bool[] matched)
        {
            var vertices = mesh.vertices;
            var centroid = Vector3.zero;
            int count = 0;
            for (int v = 0; v < matched.Length; v++)
            {
                if (!matched[v]) continue;
                centroid += vertices[v];
                count++;
            }
            centroid /= Mathf.Max(1, count);

            var deltas = new Vector3[vertices.Length];
            for (int v = 0; v < matched.Length; v++)
                if (matched[v]) deltas[v] = centroid - vertices[v];
            return deltas;
        }

        // ------------------------------------------------------------------ clips

        private static AnimationClip BuildClip(
            GameObject avatarRoot, CVRMAShapeChanger changer,
            List<(SkinnedMeshRenderer smr, string shapeName)> deleteShapes,
            bool active, string name)
        {
            var clip = new AnimationClip { name = name };
            bool any = false;

            foreach (var shape in changer.shapes)
            {
                if (shape.changeType != CVRMAShapeChangeType.Set) continue;
                if (shape.targetMesh == null || string.IsNullOrEmpty(shape.shapeName)) continue;

                var path = GetPath(avatarRoot.transform, shape.targetMesh.transform);
                if (path == null) continue;

                // Active: apply the value. Inactive: restore the mesh's CURRENT authored
                // weight — not 0, since many meshes ship with non-zero resting weights.
                float targetValue;
                if (active)
                {
                    targetValue = shape.value;
                }
                else
                {
                    int idx = shape.targetMesh.sharedMesh != null
                        ? shape.targetMesh.sharedMesh.GetBlendShapeIndex(shape.shapeName)
                        : -1;
                    if (idx < 0)
                    {
                        Debug.LogWarning(
                            $"[MA-CVR] ShapeChanger '{changer.gameObject.name}': blendshape " +
                            $"'{shape.shapeName}' not found on '{shape.targetMesh.name}' — skipped.");
                        continue;
                    }
                    targetValue = shape.targetMesh.GetBlendShapeWeight(idx);
                }

                SetShapeKey(clip, path, shape.shapeName, targetValue);
                any = true;
            }

            // Generated collapse shapes: 100 while active, 0 while not.
            foreach (var (smr, shapeName) in deleteShapes)
            {
                var path = GetPath(avatarRoot.transform, smr.transform);
                if (path == null) continue;
                SetShapeKey(clip, path, shapeName, active ? 100f : 0f);
                any = true;
            }

            if (!any)
                Debug.LogWarning($"[MA-CVR] ShapeChanger '{changer.gameObject.name}': no valid shapes to animate.");

            return clip;
        }

        private static void SetShapeKey(AnimationClip clip, string path, string shapeName, float value)
        {
            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0f,       value) { outTangent = Mathf.Infinity });
            curve.AddKey(new Keyframe(1f / 60f, value) { outTangent = Mathf.Infinity });
            clip.SetCurve(path, typeof(SkinnedMeshRenderer), $"blendShape.{shapeName}", curve);
        }

        private static string GetPath(Transform root, Transform target)
        {
            if (target == root) return "";
            var parts = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return current == null ? null : string.Join("/", parts);
        }

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "MA_CVR_Temp");
        }
    }
}
#endif
