#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Strips vertex colors from meshes under CVRMARemoveVertexColors (mode=Remove)
    /// components. The closest component to each renderer wins, so a nested
    /// DontRemove excludes its subtree. Meshes are cloned into the temp folder
    /// before modification — the user's original mesh assets are never touched.
    /// </summary>
    internal static class CVRMARemoveVertexColorsPass
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";

        internal static void Run(GameObject avatarRoot)
        {
            var components = avatarRoot.GetComponentsInChildren<CVRMARemoveVertexColors>(true);
            if (components.Length == 0) return;

            // original mesh → stripped clone, so shared meshes are processed once
            var stripped = new Dictionary<Mesh, Mesh>();
            int count = 0;

            foreach (var renderer in avatarRoot.GetComponentsInChildren<Renderer>(true))
            {
                var owner = FindClosest(renderer.transform, avatarRoot.transform);
                if (owner == null || owner.mode != CVRMARemoveVertexColorMode.Remove) continue;

                switch (renderer)
                {
                    case SkinnedMeshRenderer smr when smr.sharedMesh != null && HasColors(smr.sharedMesh):
                        smr.sharedMesh = GetStripped(smr.sharedMesh, stripped);
                        count++;
                        break;

                    case MeshRenderer _:
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter != null && filter.sharedMesh != null && HasColors(filter.sharedMesh))
                        {
                            filter.sharedMesh = GetStripped(filter.sharedMesh, stripped);
                            count++;
                        }
                        break;
                }
            }

            foreach (var comp in components)
                Object.DestroyImmediate(comp);

            if (count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[MA-CVR] RemoveVertexColors: stripped colors from {count} renderer(s).");
            }
        }

        private static bool HasColors(Mesh mesh) => mesh.colors32 != null && mesh.colors32.Length > 0;

        private static Mesh GetStripped(Mesh source, Dictionary<Mesh, Mesh> cache)
        {
            if (cache.TryGetValue(source, out var existing)) return existing;

            var clone = Object.Instantiate(source);
            clone.name = source.name + "_NoVC";
            clone.colors32 = null;

            EnsureTempFolder();
            var path = $"{TempFolder}/MA_CVR_{clone.name}_{GUID.Generate()}.asset";
            AssetDatabase.CreateAsset(clone, path);

            cache[source] = clone;
            return clone;
        }

        /// <summary>Walks up from the renderer to find the nearest component (closest wins).</summary>
        private static CVRMARemoveVertexColors FindClosest(Transform from, Transform stopAfter)
        {
            var current = from;
            while (current != null)
            {
                var c = current.GetComponent<CVRMARemoveVertexColors>();
                if (c != null) return c;
                if (current == stopAfter) break;
                current = current.parent;
            }
            return null;
        }

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "MA_CVR_Temp");
        }
    }
}
#endif
