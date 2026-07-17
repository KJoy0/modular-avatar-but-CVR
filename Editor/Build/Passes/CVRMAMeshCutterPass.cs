#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Applies each CVRMAMeshCutter at build time.
    ///
    /// Delete: the target mesh is cloned into the temp folder with the selected
    /// triangles removed (original asset untouched).
    ///
    /// Toggle: the selected triangles are split into a sibling child renderer and a
    /// CVRMAObjectToggle is generated to drive it — the later ObjectToggle pass turns
    /// that into the AAS entry + animator layer.
    ///
    /// Must run after MergeArmature (bones final) and before ObjectToggle.
    /// </summary>
    internal static class CVRMAMeshCutterPass
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";

        internal static void Run(GameObject avatarRoot)
        {
            var cutters = avatarRoot.GetComponentsInChildren<CVRMAMeshCutter>(true);
            if (cutters.Length == 0) return;

            int applied = 0;
            foreach (var cutter in cutters)
            {
                try
                {
                    if (Apply(cutter)) applied++;
                }
                finally
                {
                    Object.DestroyImmediate(cutter);
                }
            }

            if (applied > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[MA-CVR] MeshCutter: applied {applied} cutter(s).");
            }
        }

        private static bool Apply(CVRMAMeshCutter cutter)
        {
            if (!CVRMAMeshCutterUtil.ComputeCut(cutter, out var kept, out var cut, out int cutTris, out _))
                return false;

            var smr = cutter.GetEffectiveTarget();
            var mesh = smr.sharedMesh;

            if (cutTris == 0)
            {
                Debug.LogWarning(
                    $"[MA-CVR] MeshCutter on '{cutter.gameObject.name}': filters selected no complete " +
                    "triangles — nothing to cut.");
                return false;
            }

            if (cutter.action == CVRMAMeshCutterAction.Delete)
            {
                var newMesh = CVRMAMeshCutterUtil.BuildCompactedMesh(mesh, kept, mesh.name + "_Cut");
                SaveTempMesh(newMesh);
                smr.sharedMesh = newMesh;
                Debug.Log(
                    $"[MA-CVR] MeshCutter '{cutter.gameObject.name}': deleted {cutTris} tri(s) from " +
                    $"'{smr.name}' ({mesh.vertexCount} → {newMesh.vertexCount} verts).");
            }
            else
            {
                ApplyToggle(cutter, smr, mesh, kept, cut, cutTris);
            }
            return true;
        }

        private static void ApplyToggle(
            CVRMAMeshCutter cutter, SkinnedMeshRenderer smr, Mesh mesh,
            System.Collections.Generic.List<int>[] kept,
            System.Collections.Generic.List<int>[] cut, int cutTris)
        {
            var machineName = cutter.GetEffectiveParameter();

            var baseMesh = CVRMAMeshCutterUtil.BuildCompactedMesh(mesh, kept, mesh.name + "_Base");
            var cutMesh  = CVRMAMeshCutterUtil.BuildCompactedMesh(mesh, cut,  mesh.name + "_" + machineName);
            SaveTempMesh(baseMesh);
            SaveTempMesh(cutMesh);

            smr.sharedMesh = baseMesh;

            // Sibling child renderer carrying the cut portion — same skeleton, same materials.
            var child = new GameObject($"{smr.name}_{machineName}")
            {
                layer = smr.gameObject.layer
            };
            child.transform.SetParent(smr.transform.parent, false);
            child.transform.localPosition = smr.transform.localPosition;
            child.transform.localRotation = smr.transform.localRotation;
            child.transform.localScale    = smr.transform.localScale;

            var childSmr = child.AddComponent<SkinnedMeshRenderer>();
            childSmr.sharedMesh          = cutMesh;
            childSmr.sharedMaterials     = smr.sharedMaterials;
            childSmr.bones               = smr.bones;
            childSmr.rootBone            = smr.rootBone;
            childSmr.localBounds         = smr.localBounds;
            childSmr.probeAnchor         = smr.probeAnchor;
            childSmr.quality             = smr.quality;
            childSmr.updateWhenOffscreen = smr.updateWhenOffscreen;
            childSmr.shadowCastingMode   = smr.shadowCastingMode;
            childSmr.receiveShadows      = smr.receiveShadows;
            childSmr.lightProbeUsage     = smr.lightProbeUsage;
            childSmr.reflectionProbeUsage = smr.reflectionProbeUsage;

            // Mirror the source's current blendshape weights (indices are preserved).
            for (int i = 0; i < cutMesh.blendShapeCount && i < mesh.blendShapeCount; i++)
                childSmr.SetBlendShapeWeight(i, smr.GetBlendShapeWeight(i));

            // Scene state must match the toggle's default so the avatar loads consistent.
            child.SetActive(cutter.defaultValue != cutter.hiddenWhenOn);

            var toggle = cutter.gameObject.AddComponent<CVRMAObjectToggle>();
            toggle.label        = cutter.GetEffectiveLabel();
            toggle.parameter    = machineName;
            toggle.defaultValue = cutter.defaultValue;
            toggle.objects.Add(new CVRMAToggledObject
            {
                target      = child.transform,
                activeWhenOn = !cutter.hiddenWhenOn
            });

            Debug.Log(
                $"[MA-CVR] MeshCutter '{cutter.gameObject.name}': split {cutTris} tri(s) of '{smr.name}' " +
                $"into toggleable '{child.name}' (parameter '{machineName}').");
        }

        private static void SaveTempMesh(Mesh mesh)
        {
            EnsureTempFolder();
            AssetDatabase.CreateAsset(mesh, $"{TempFolder}/MA_CVR_{mesh.name}_{GUID.Generate()}.asset");
        }

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "MA_CVR_Temp");
        }
    }
}
#endif
