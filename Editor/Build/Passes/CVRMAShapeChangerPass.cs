#if UNITY_EDITOR
using System.Collections.Generic;
using ABI.CCK.Scripts;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Converts each CVRMAShapeChanger into a parameter-driven animator layer merged
    /// into the avatar's controller, plus a slim AAS menu entry.
    ///
    /// Set entries animate the blendshape to the given value while active.
    ///
    /// Delete entries mirror VRC MA: the polygons DISPLACED by the blendshape
    /// (beyond the displacement threshold) are collapsed while active. Since meshes
    /// can't be re-cut at runtime, the pass clones the mesh and adds a generated
    /// blendshape that collapses the affected vertices, then animates that shape
    /// 0 ↔ 100 with the toggle.
    ///
    /// See CVRMAAnimatorUtil for why AAS useAnimationClip can't be used.
    /// </summary>
    internal static class CVRMAShapeChangerPass
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";

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

                var machineName = changer.GetEffectiveParameter();

                // Delete entries first: clone meshes and generate collapse blendshapes.
                var deleteShapes = PrepareDeleteShapes(changer, machineName);

                var activeClip   = BuildClip(avatarRoot, changer, deleteShapes, active: true,  name: $"{machineName}_ShapeOn");
                var inactiveClip = BuildClip(avatarRoot, changer, deleteShapes, active: false, name: $"{machineName}_ShapeOff");

                // Inverse Condition swaps which clip plays in the parameter's ON state.
                var onClip  = changer.inverseCondition ? inactiveClip : activeClip;
                var offClip = changer.inverseCondition ? activeClip   : inactiveClip;

                // Build a real animator layer and merge it (the AAS clips path is a dead end).
                var (paramType, compareValue, menuOwned) =
                    CVRMAAnimatorUtil.ResolveMenuBinding(changer.gameObject, machineName);
                var controller = CVRMAAnimatorUtil.BuildToggleController(
                    machineName, onClip, offClip, changer.defaultValue, paramType, compareValue);
                if (controller != null)
                    CVRMAAnimatorUtil.InjectMergeAnimator(changer.gameObject, controller);

                // Register the menu parameter (one entry per machine name) — unless an
                // Int/Float menu item owns it (the MenuToAAS pass creates that entry).
                if (!menuOwned)
                    CVRMAAASUtil.AddOrMergeToggleEntry(avatar,
                        CVRMAAnimatorUtil.BuildMenuEntry(changer.gameObject.name, machineName, changer.defaultValue));

                Object.DestroyImmediate(changer);
            }
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

            var byMesh = new Dictionary<SkinnedMeshRenderer, List<string>>();
            foreach (var shape in changer.shapes)
            {
                if (shape.changeType != CVRMAShapeChangeType.Delete) continue;
                if (shape.targetMesh == null || string.IsNullOrEmpty(shape.shapeName)) continue;
                if (!byMesh.TryGetValue(shape.targetMesh, out var list))
                    byMesh[shape.targetMesh] = list = new List<string>();
                list.Add(shape.shapeName);
            }

            foreach (var kv in byMesh)
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
