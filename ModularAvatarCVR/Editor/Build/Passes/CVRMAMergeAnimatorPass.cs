#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    internal static class CVRMAMergeAnimatorPass
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";
        private static readonly List<string> _tempAssets = new List<string>();

        internal static void Run(GameObject avatarRoot)
        {
            var mergers = avatarRoot.GetComponentsInChildren<CVRMAMergeAnimator>(true)
                .OrderBy(m => m.layerPriority)
                .ToList();

            if (mergers.Count == 0) return;

            EnsureTempFolder();

            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            if (avatar == null)
            {
                Debug.LogWarning("[MA-CVR] MergeAnimator: no CVRAvatar found on root — skipped.");
                return;
            }

            // Determine base controller: prefer existing overrides base, else create new
            AnimatorController mergedController = GetOrCreateBaseController(avatar);

            foreach (var merger in mergers)
            {
                if (merger.animator == null) continue;

                var sourceController = merger.animator as AnimatorController
                    ?? AssetDatabase.LoadAssetAtPath<AnimatorController>(
                        AssetDatabase.GetAssetPath(merger.animator));

                if (sourceController == null)
                {
                    Debug.LogWarning($"[MA-CVR] MergeAnimator: '{merger.gameObject.name}' animator is not an AnimatorController — skipped.", merger);
                    continue;
                }

                if (merger.mergeMode == CVRMAMergeMode.Replace)
                {
                    mergedController = DuplicateController(sourceController);
                }
                else
                {
                    var relPath = merger.pathMode == CVRMAPathMode.Relative
                        ? GetRelativePath(avatarRoot.transform, merger.transform)
                        : "";

                    AppendLayers(sourceController, mergedController, relPath, merger.matchAvatarWriteDefaults);
                }

                if (merger.deleteAttachedAnimator)
                {
                    var attached = merger.GetComponent<Animator>();
                    if (attached != null) Object.DestroyImmediate(attached);
                }

                Object.DestroyImmediate(merger);
            }

            // Apply merged controller to avatar
            ApplyToAvatar(avatar, mergedController);
        }

        private static AnimatorController GetOrCreateBaseController(ABI.CCK.Components.CVRAvatar avatar)
        {
            if (avatar.overrides != null && avatar.overrides.runtimeAnimatorController is AnimatorController existing)
                return DuplicateController(existing);

            var ctrl = new AnimatorController();
            ctrl.name = "MA_CVR_Merged";
            var path = $"{TempFolder}/MA_CVR_Merged_{GUID.Generate()}.controller";
            AssetDatabase.CreateAsset(ctrl, path);
            _tempAssets.Add(path);
            return ctrl;
        }

        private static AnimatorController DuplicateController(AnimatorController source)
        {
            var path = $"{TempFolder}/MA_CVR_{source.name}_{GUID.Generate()}.controller";
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path);
            _tempAssets.Add(path);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }

        private static void AppendLayers(AnimatorController source, AnimatorController dest, string pathPrefix, bool matchWriteDefaults)
        {
            // Copy parameters first (avoid duplicates)
            var existingParams = new HashSet<string>(dest.parameters.Select(p => p.name));
            foreach (var param in source.parameters)
            {
                if (existingParams.Add(param.name))
                    dest.AddParameter(param.name, param.type);
            }

            foreach (var layer in source.layers)
            {
                var newLayer = DuplicateLayer(layer, dest, pathPrefix, matchWriteDefaults, source);
                dest.AddLayer(newLayer);
            }
        }

        private static AnimatorControllerLayer DuplicateLayer(
            AnimatorControllerLayer sourceLayer,
            AnimatorController destController,
            string pathPrefix,
            bool matchWriteDefaults,
            AnimatorController sourceController)
        {
            // Deep-copy the state machine into the destination controller asset
            var smCopy = Object.Instantiate(sourceLayer.stateMachine);
            smCopy.name = sourceLayer.name;

            // Remap animation clip paths and optionally fix write defaults
            if (!string.IsNullOrEmpty(pathPrefix) || matchWriteDefaults)
            {
                RemapStateMachine(smCopy, pathPrefix, matchWriteDefaults);
            }

            AssetDatabase.AddObjectToAsset(smCopy, destController);
            smCopy.hideFlags = HideFlags.HideInHierarchy;

            var newLayer = new AnimatorControllerLayer
            {
                name = sourceLayer.name,
                defaultWeight = sourceLayer.defaultWeight,
                blendingMode = sourceLayer.blendingMode,
                stateMachine = smCopy
            };

            return newLayer;
        }

        private static void RemapStateMachine(AnimatorStateMachine sm, string pathPrefix, bool matchWriteDefaults)
        {
            foreach (var state in sm.states)
            {
                if (matchWriteDefaults)
                    state.state.writeDefaultValues = true;

                state.state.motion = RemapMotion(state.state.motion, pathPrefix);
            }

            foreach (var subSm in sm.stateMachines)
                RemapStateMachine(subSm.stateMachine, pathPrefix, matchWriteDefaults);
        }

        private static Motion RemapMotion(Motion motion, string pathPrefix)
        {
            if (string.IsNullOrEmpty(pathPrefix)) return motion;
            if (motion == null) return null;

            if (motion is AnimationClip clip)
                return RemapClip(clip, pathPrefix);

            if (motion is BlendTree blendTree)
            {
                var children = blendTree.children;
                for (var i = 0; i < children.Length; i++)
                    children[i].motion = RemapMotion(children[i].motion, pathPrefix);
                blendTree.children = children;
                return blendTree;
            }

            return motion;
        }

        private static AnimationClip RemapClip(AnimationClip sourceClip, string pathPrefix)
        {
            var newClip = Object.Instantiate(sourceClip);
            newClip.name = sourceClip.name;
            newClip.ClearCurves();

            foreach (var binding in AnimationUtility.GetCurveBindings(sourceClip))
            {
                var newBinding = binding;
                newBinding.path = string.IsNullOrEmpty(binding.path)
                    ? pathPrefix
                    : pathPrefix + "/" + binding.path;
                newClip.SetCurve(newBinding.path, newBinding.type, newBinding.propertyName,
                    AnimationUtility.GetEditorCurve(sourceClip, binding));
            }

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(sourceClip))
            {
                var newBinding = binding;
                newBinding.path = string.IsNullOrEmpty(binding.path)
                    ? pathPrefix
                    : pathPrefix + "/" + binding.path;
                AnimationUtility.SetObjectReferenceCurve(newClip, newBinding,
                    AnimationUtility.GetObjectReferenceCurve(sourceClip, binding));
            }

            var clipPath = $"{TempFolder}/MA_CVR_{sourceClip.name}_{GUID.Generate()}.anim";
            AssetDatabase.CreateAsset(newClip, clipPath);
            _tempAssets.Add(clipPath);
            return newClip;
        }

        private static void ApplyToAvatar(ABI.CCK.Components.CVRAvatar avatar, AnimatorController controller)
        {
            if (avatar.overrides == null)
            {
                avatar.overrides = new AnimatorOverrideController(controller);
            }
            else
            {
                avatar.overrides = new AnimatorOverrideController(controller);
            }

            // Keep AAS base controller in sync for autogen
            if (avatar.avatarSettings != null)
                avatar.avatarSettings.baseController = controller;
        }

        internal static void Cleanup()
        {
            foreach (var path in _tempAssets)
            {
                if (AssetDatabase.AssetPathToGUID(path) != "")
                    AssetDatabase.DeleteAsset(path);
            }

            _tempAssets.Clear();

            if (AssetDatabase.IsValidFolder(TempFolder) &&
                AssetDatabase.FindAssets("", new[] { TempFolder }).Length == 0)
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }

            AssetDatabase.Refresh();
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root) return "";
            var path = target.name;
            var current = target.parent;
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "MA_CVR_Temp");
        }
    }
}
#endif
