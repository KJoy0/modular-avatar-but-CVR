#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Vertex/triangle-selection and mesh-rebuild helpers for CVRMAMeshCutter.
    /// Shared by the build pass and the inspector's selection preview.
    ///
    /// Pipeline (mirrors VRC MA): each filter produces a per-vertex match, its
    /// selection mode lifts that to per-triangle, and the cutter's mode combines
    /// the filters' triangle sets by union or intersection.
    /// </summary>
    internal static class CVRMAMeshCutterUtil
    {
        /// <summary>
        /// Evaluates the cutter and partitions every submesh's triangles into kept
        /// vs cut. Returns false (with a warning) when nothing could be evaluated.
        /// <paramref name="cutVertices"/> flags the vertices used by cut triangles
        /// (for the inspector preview).
        /// </summary>
        internal static bool ComputeCut(
            CVRMAMeshCutter cutter,
            out List<int>[] kept, out List<int>[] cut, out int cutTris, out bool[] cutVertices)
        {
            kept = null; cut = null; cutTris = 0; cutVertices = null;

            var smr = cutter.GetEffectiveTarget();
            if (smr == null || smr.sharedMesh == null)
            {
                Debug.LogWarning($"[MA-CVR] MeshCutter on '{cutter.gameObject.name}': no target mesh.");
                return false;
            }
            if (cutter.filters == null || cutter.filters.Count == 0)
            {
                Debug.LogWarning($"[MA-CVR] MeshCutter on '{cutter.gameObject.name}': no vertex filters configured.");
                return false;
            }

            var mesh = smr.sharedMesh;

            // Per-filter vertex matches (invert applied), plus each filter's triangle mode.
            var vertexMatches = new List<bool[]>();
            var triModes = new List<CVRMATriangleSelectMode>();
            foreach (var filter in cutter.filters)
            {
                var match = EvaluateFilter(filter, mesh, smr, cutter.gameObject.name);
                if (match == null) continue; // misconfigured filter — already warned

                if (filter.invert)
                    for (int i = 0; i < match.Length; i++) match[i] = !match[i];

                vertexMatches.Add(match);
                triModes.Add(filter.selectionMode);
            }

            if (vertexMatches.Count == 0)
            {
                Debug.LogWarning($"[MA-CVR] MeshCutter on '{cutter.gameObject.name}': no filter could be evaluated — nothing selected.");
                return false;
            }

            bool intersect = cutter.mode == CVRMAMeshCutterMode.VertexIntersection;
            int subCount = mesh.subMeshCount;
            kept = new List<int>[subCount];
            cut  = new List<int>[subCount];
            cutVertices = new bool[mesh.vertexCount];

            for (int s = 0; s < subCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                kept[s] = new List<int>(tris.Length);
                cut[s]  = new List<int>();

                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];

                    bool selected = intersect;
                    for (int f = 0; f < vertexMatches.Count; f++)
                    {
                        bool triMatch = TriangleMatches(vertexMatches[f], triModes[f], a, b, c);
                        if (intersect) selected &= triMatch;
                        else           selected |= triMatch;
                        if (intersect != selected) break; // early out once decided
                    }

                    if (selected)
                    {
                        cut[s].Add(a); cut[s].Add(b); cut[s].Add(c);
                        cutVertices[a] = cutVertices[b] = cutVertices[c] = true;
                        cutTris++;
                    }
                    else
                    {
                        kept[s].Add(a); kept[s].Add(b); kept[s].Add(c);
                    }
                }
            }
            return true;
        }

        private static bool TriangleMatches(bool[] match, CVRMATriangleSelectMode mode, int a, int b, int c)
        {
            switch (mode)
            {
                case CVRMATriangleSelectMode.AllVertices:
                    return match[a] && match[b] && match[c];
                case CVRMATriangleSelectMode.Centroid:
                    // Majority vote approximates testing the triangle's centroid.
                    return (match[a] ? 1 : 0) + (match[b] ? 1 : 0) + (match[c] ? 1 : 0) >= 2;
                default: // AnyVertex
                    return match[a] || match[b] || match[c];
            }
        }

        // ------------------------------------------------------------------ filters

        private static bool[] EvaluateFilter(
            CVRMAVertexFilter filter, Mesh mesh, SkinnedMeshRenderer smr, string owner)
        {
            switch (filter.type)
            {
                case CVRMAVertexFilterType.ByBone:       return ByBone(filter, mesh, smr, owner);
                case CVRMAVertexFilterType.ByBlendshape: return ByBlendshape(filter, mesh, owner);
                case CVRMAVertexFilterType.ByAxis:       return ByAxis(filter, mesh);
                case CVRMAVertexFilterType.ByMask:       return ByMask(filter, mesh, owner);
                case CVRMAVertexFilterType.ByUVTile:     return ByUVTile(filter, mesh, owner);
                default: return null;
            }
        }

        private static bool[] ByBone(CVRMAVertexFilter filter, Mesh mesh, SkinnedMeshRenderer smr, string owner)
        {
            if (filter.bone == null)
            {
                Debug.LogWarning($"[MA-CVR] MeshCutter on '{owner}': ByBone filter has no bone assigned — skipped.");
                return null;
            }

            var bones = smr.bones;
            var boneMatches = new bool[bones.Length];
            bool any = false;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                boneMatches[i] = bones[i] == filter.bone ||
                                 (filter.includeChildBones && bones[i].IsChildOf(filter.bone));
                any |= boneMatches[i];
            }
            if (!any)
            {
                Debug.LogWarning(
                    $"[MA-CVR] MeshCutter on '{owner}': ByBone filter bone '{filter.bone.name}' " +
                    "is not part of the target mesh's skeleton — skipped.");
                return null;
            }

            var weights = mesh.boneWeights;
            if (weights == null || weights.Length != mesh.vertexCount)
            {
                Debug.LogWarning($"[MA-CVR] MeshCutter on '{owner}': target mesh has no skin weights — ByBone skipped.");
                return null;
            }

            var result = new bool[mesh.vertexCount];
            for (int v = 0; v < weights.Length; v++)
            {
                var w = weights[v];
                float total = 0f;
                if (boneMatches[w.boneIndex0]) total += w.weight0;
                if (boneMatches[w.boneIndex1]) total += w.weight1;
                if (boneMatches[w.boneIndex2]) total += w.weight2;
                if (boneMatches[w.boneIndex3]) total += w.weight3;
                result[v] = total >= filter.minBoneWeight;
            }
            return result;
        }

        private static bool[] ByBlendshape(CVRMAVertexFilter filter, Mesh mesh, string owner)
        {
            var shapes = new List<int>();
            if (filter.blendshapeNames != null)
            {
                foreach (var name in filter.blendshapeNames)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    int idx = mesh.GetBlendShapeIndex(name);
                    if (idx >= 0) shapes.Add(idx);
                    else Debug.LogWarning(
                        $"[MA-CVR] MeshCutter on '{owner}': blendshape '{name}' not found on target mesh — ignored.");
                }
            }
            if (shapes.Count == 0)
            {
                Debug.LogWarning($"[MA-CVR] MeshCutter on '{owner}': ByBlendshape filter matched no blendshapes — skipped.");
                return null;
            }

            var deltas = new Vector3[mesh.vertexCount];
            var normals = new Vector3[mesh.vertexCount];
            var tangents = new Vector3[mesh.vertexCount];
            float minSqr = filter.minShapeDelta * filter.minShapeDelta;

            var result = new bool[mesh.vertexCount];
            foreach (var shape in shapes)
            {
                int lastFrame = mesh.GetBlendShapeFrameCount(shape) - 1;
                mesh.GetBlendShapeFrameVertices(shape, lastFrame, deltas, normals, tangents);
                for (int v = 0; v < deltas.Length; v++)
                    result[v] |= deltas[v].sqrMagnitude >= minSqr;
            }
            return result;
        }

        private static bool[] ByAxis(CVRMAVertexFilter filter, Mesh mesh)
        {
            var normal = filter.planeNormal.sqrMagnitude > 0f ? filter.planeNormal.normalized : Vector3.up;
            var vertices = mesh.vertices;
            var result = new bool[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
                result[v] = Vector3.Dot(vertices[v] - filter.planeOrigin, normal) >= 0f;
            return result;
        }

        private static bool[] ByMask(CVRMAVertexFilter filter, Mesh mesh, string owner)
        {
            if (filter.maskTexture == null)
            {
                Debug.LogWarning($"[MA-CVR] MeshCutter on '{owner}': ByMask filter has no mask texture — skipped.");
                return null;
            }
            var uvs = GetUVs(mesh, filter.uvChannel, owner);
            if (uvs == null) return null;

            var readable = GetReadableCopy(filter.maskTexture);
            bool selectWhite = filter.maskMode == CVRMAMaskSelectMode.SelectWhite;

            var result = new bool[mesh.vertexCount];
            for (int v = 0; v < uvs.Count; v++)
            {
                // Repeat-wrap so tiled UVs sample sensibly.
                float u = Mathf.Repeat(uvs[v].x, 1f);
                float w = Mathf.Repeat(uvs[v].y, 1f);
                bool white = readable.GetPixelBilinear(u, w).grayscale >= 0.5f;
                result[v] = white == selectWhite;
            }

            // Material slot scoping: only vertices used by that submesh stay matched.
            if (filter.materialSlot >= 0)
            {
                if (filter.materialSlot >= mesh.subMeshCount)
                {
                    Debug.LogWarning(
                        $"[MA-CVR] MeshCutter on '{owner}': ByMask material slot {filter.materialSlot} " +
                        $"out of range (mesh has {mesh.subMeshCount}) — skipped.");
                    return null;
                }
                var inSlot = new bool[mesh.vertexCount];
                foreach (var idx in mesh.GetTriangles(filter.materialSlot))
                    inSlot[idx] = true;
                for (int v = 0; v < result.Length; v++)
                    result[v] &= inSlot[v];
            }

            if (readable != filter.maskTexture)
                Object.DestroyImmediate(readable);
            return result;
        }

        private static bool[] ByUVTile(CVRMAVertexFilter filter, Mesh mesh, string owner)
        {
            var uvs = GetUVs(mesh, filter.uvChannel, owner);
            if (uvs == null) return null;

            var result = new bool[mesh.vertexCount];
            for (int v = 0; v < uvs.Count; v++)
                result[v] = Mathf.FloorToInt(uvs[v].x) == filter.tileX &&
                            Mathf.FloorToInt(uvs[v].y) == filter.tileY;
            return result;
        }

        private static List<Vector2> GetUVs(Mesh mesh, int channel, string owner)
        {
            var uvs = new List<Vector2>();
            mesh.GetUVs(channel, uvs);
            if (uvs.Count != mesh.vertexCount)
            {
                Debug.LogWarning($"[MA-CVR] MeshCutter on '{owner}': target mesh has no UV{channel} channel — filter skipped.");
                return null;
            }
            return uvs;
        }

        /// <summary>Returns a CPU-readable copy of a texture (or the texture itself if already readable).</summary>
        private static Texture2D GetReadableCopy(Texture2D tex)
        {
            if (tex.isReadable) return tex;

            var rt = RenderTexture.GetTemporary(
                tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, true);
            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        // ------------------------------------------------------------------ mesh rebuild

        /// <summary>
        /// Builds a new mesh containing only the given triangles (indices into the
        /// source mesh), with unused vertices compacted away. Preserves submesh
        /// structure (empty submeshes kept so material slots stay aligned), skin
        /// weights, bindposes, all UV channels, colors, and every blendshape.
        /// </summary>
        internal static Mesh BuildCompactedMesh(Mesh src, List<int>[] trianglesPerSubmesh, string name)
        {
            // Old → new vertex index map, in first-use order.
            var map = new int[src.vertexCount];
            for (int i = 0; i < map.Length; i++) map[i] = -1;
            var used = new List<int>();

            foreach (var tris in trianglesPerSubmesh)
                foreach (var idx in tris)
                    if (map[idx] < 0) { map[idx] = used.Count; used.Add(idx); }

            int newCount = used.Count;
            var dst = new Mesh
            {
                name = name,
                indexFormat = newCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            dst.vertices = Remap(src.vertices, used);
            var normals = src.normals;
            if (normals != null && normals.Length == src.vertexCount) dst.normals = Remap(normals, used);
            var tangents = src.tangents;
            if (tangents != null && tangents.Length == src.vertexCount) dst.tangents = Remap(tangents, used);
            var colors = src.colors32;
            if (colors != null && colors.Length == src.vertexCount) dst.colors32 = Remap(colors, used);

            for (int ch = 0; ch < 4; ch++)
            {
                var uvs = new List<Vector4>();
                src.GetUVs(ch, uvs);
                if (uvs.Count != src.vertexCount) continue;
                var remapped = new List<Vector4>(newCount);
                foreach (var oldIdx in used) remapped.Add(uvs[oldIdx]);
                dst.SetUVs(ch, remapped);
            }

            var weights = src.boneWeights;
            if (weights != null && weights.Length == src.vertexCount)
            {
                dst.boneWeights = Remap(weights, used);
                dst.bindposes = src.bindposes;
            }

            dst.subMeshCount = trianglesPerSubmesh.Length;
            for (int s = 0; s < trianglesPerSubmesh.Length; s++)
            {
                var tris = trianglesPerSubmesh[s];
                var remapped = new int[tris.Count];
                for (int i = 0; i < tris.Count; i++) remapped[i] = map[tris[i]];
                dst.SetTriangles(remapped, s, calculateBounds: false);
            }

            CopyBlendshapes(src, dst, used);

            dst.RecalculateBounds();
            return dst;
        }

        private static void CopyBlendshapes(Mesh src, Mesh dst, List<int> used)
        {
            int srcCount = src.vertexCount;
            var dv = new Vector3[srcCount];
            var dn = new Vector3[srcCount];
            var dt = new Vector3[srcCount];

            for (int shape = 0; shape < src.blendShapeCount; shape++)
            {
                string shapeName = src.GetBlendShapeName(shape);
                int frames = src.GetBlendShapeFrameCount(shape);
                for (int frame = 0; frame < frames; frame++)
                {
                    src.GetBlendShapeFrameVertices(shape, frame, dv, dn, dt);
                    dst.AddBlendShapeFrame(shapeName,
                        src.GetBlendShapeFrameWeight(shape, frame),
                        Remap(dv, used), Remap(dn, used), Remap(dt, used));
                }
            }
        }

        private static T[] Remap<T>(T[] source, List<int> used)
        {
            var result = new T[used.Count];
            for (int i = 0; i < used.Count; i++) result[i] = source[used[i]];
            return result;
        }
    }
}
#endif
