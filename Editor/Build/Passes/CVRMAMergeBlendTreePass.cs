#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Wraps each CVRMAMergeBlendTree motion in a single-state AnimatorController layer,
    /// then converts it to a CVRMAMergeAnimator so the existing animator-merge pass handles it.
    /// </summary>
    internal static class CVRMAMergeBlendTreePass
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";

        internal static void Run(GameObject avatarRoot)
        {
            var blendTrees = avatarRoot.GetComponentsInChildren<CVRMAMergeBlendTree>(true);
            if (blendTrees.Length == 0) return;

            EnsureTempFolder();

            foreach (var bt in blendTrees)
            {
                if (bt.motion == null)
                {
                    Object.DestroyImmediate(bt);
                    continue;
                }

                var ctrl = WrapInController(bt);
                if (ctrl == null)
                {
                    Object.DestroyImmediate(bt);
                    continue;
                }

                // Inject a CVRMAMergeAnimator so the existing pass handles the rest
                var merger = bt.gameObject.AddComponent<CVRMAMergeAnimator>();
                merger.animator      = ctrl;
                merger.pathMode      = bt.pathMode;
                merger.layerPriority = bt.layerPriority;
                merger.deleteAttachedAnimator  = false;
                merger.matchAvatarWriteDefaults = true;

                Object.DestroyImmediate(bt);
            }
        }

        private static AnimatorController WrapInController(CVRMAMergeBlendTree bt)
        {
            var layerName = string.IsNullOrEmpty(bt.layerName)
                ? $"MA_BlendTree_{bt.gameObject.name}"
                : bt.layerName;

            var ctrl = new AnimatorController { name = layerName };

            var sm = new AnimatorStateMachine { name = layerName };
            var state = sm.AddState(layerName);
            state.motion = bt.motion;

            var layer = new AnimatorControllerLayer
            {
                name          = layerName,
                defaultWeight = 1f,
                stateMachine  = sm
            };

            var path = $"{TempFolder}/MA_CVR_{layerName}_{GUID.Generate()}.controller";
            AssetDatabase.CreateAsset(ctrl, path);
            AssetDatabase.AddObjectToAsset(sm, ctrl);
            sm.hideFlags = HideFlags.HideInHierarchy;

            ctrl.AddLayer(layer);
            AssetDatabase.SaveAssets();

            return ctrl;
        }

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "MA_CVR_Temp");
        }
    }
}
#endif
