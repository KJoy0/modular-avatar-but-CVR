#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Generates an animator layer that drives target blendshapes from source blendshapes,
    /// then injects it into the avatar's animator via CVRMAMergeAnimatorPass.
    /// </summary>
    internal static class CVRMABlendshapeSyncPass
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";

        internal static void Run(GameObject avatarRoot)
        {
            var syncs = avatarRoot.GetComponentsInChildren<CVRMABlendshapeSync>(true);
            if (syncs.Length == 0) return;

            EnsureTempFolder();

            foreach (var sync in syncs)
            {
                if (sync.blendshapes == null || sync.blendshapes.Count == 0) continue;

                var controller = BuildSyncController(avatarRoot, sync);
                if (controller == null) continue;

                // Inject a merge animator component to pick it up in the animator pass
                var merger = sync.gameObject.AddComponent<CVRMAMergeAnimator>();
                merger.animator = controller;
                merger.pathMode = CVRMAPathMode.Absolute;
                merger.deleteAttachedAnimator = false;
                merger.layerPriority = 1000; // run last

                Object.DestroyImmediate(sync);
            }
        }

        private static AnimatorController BuildSyncController(GameObject avatarRoot, CVRMABlendshapeSync sync)
        {
            var ctrl = new AnimatorController();
            ctrl.name = $"MA_CVR_BlendshapeSync_{sync.gameObject.name}";

            foreach (var entry in sync.blendshapes)
            {
                if (entry.localMesh == null || entry.targetMesh == null) continue;
                if (string.IsNullOrEmpty(entry.localBlendshape) || string.IsNullOrEmpty(entry.targetBlendshape)) continue;

                var localPath = GetPath(avatarRoot.transform, entry.localMesh.transform);
                var targetPath = GetPath(avatarRoot.transform, entry.targetMesh.transform);
                if (localPath == null || targetPath == null) continue;

                var paramName = $"MA_BS_{entry.localBlendshape}";

                ctrl.AddParameter(paramName, AnimatorControllerParameterType.Float);

                var layer = new AnimatorControllerLayer
                {
                    name = paramName,
                    defaultWeight = 1f,
                    stateMachine = new AnimatorStateMachine()
                };
                layer.stateMachine.name = paramName;

                var state = layer.stateMachine.AddState(paramName + " Blend");

                var blendTree = new BlendTree { name = paramName, blendParameter = paramName };

                var minClip = MakeBlendshapeClip(targetPath, entry.targetBlendshape, 0f, $"{paramName}_0");
                var maxClip = MakeBlendshapeClip(targetPath, entry.targetBlendshape, 100f, $"{paramName}_100");

                blendTree.AddChild(minClip, 0f);
                blendTree.AddChild(maxClip, 100f);
                state.motion = blendTree;

                ctrl.AddLayer(layer);
            }

            if (ctrl.layers.Length == 0) return null;

            var path = $"{TempFolder}/{ctrl.name}_{GUID.Generate()}.controller";
            AssetDatabase.CreateAsset(ctrl, path);

            foreach (var layer in ctrl.layers)
            {
                AssetDatabase.AddObjectToAsset(layer.stateMachine, ctrl);
                layer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
            }

            AssetDatabase.SaveAssets();
            return ctrl;
        }

        private static AnimationClip MakeBlendshapeClip(string meshPath, string blendshapeName, float value, string clipName)
        {
            var clip = new AnimationClip { name = clipName };
            var curve = AnimationCurve.Constant(0f, 1f / 60f, value);
            clip.SetCurve(meshPath, typeof(SkinnedMeshRenderer), $"blendShape.{blendshapeName}", curve);
            var path = $"{TempFolder}/{clipName}_{GUID.Generate()}.anim";
            AssetDatabase.CreateAsset(clip, path);
            return clip;
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
