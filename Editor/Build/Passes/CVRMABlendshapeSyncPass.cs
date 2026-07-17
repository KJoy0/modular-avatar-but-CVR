#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Makes outfit blendshapes follow base-avatar blendshapes, the same way VRC MA does:
    /// by RETARGETING ANIMATION CURVES rather than trying to drive a parameter (animators
    /// cannot read a blendshape weight into a parameter, so a parameter-based layer would
    /// silently do nothing at runtime).
    ///
    /// MUST run AFTER CVRMAMergeAnimatorPass — it post-processes the final merged
    /// controller: every clip that animates a followed (base) blendshape gets an identical
    /// curve added for each follower (outfit) blendshape. AAS Toggle clips are processed
    /// the same way. Clips are cloned before modification so the user's original .anim
    /// assets are never touched.
    ///
    /// Direction: targetMesh.targetBlendshape (base avatar mesh, source of truth)
    ///         →  localMesh.localBlendshape  (this outfit mesh, follower).
    /// The static (un-animated) weight is also copied once at build time.
    /// </summary>
    internal static class CVRMABlendshapeSyncPass
    {
        private const string TempFolder = "Assets/MA_CVR_Temp";

        internal static void Run(GameObject avatarRoot)
        {
            var syncs = avatarRoot.GetComponentsInChildren<CVRMABlendshapeSync>(true);
            if (syncs.Length == 0) return;

            // sourceKey = "path|blendshapeName" on the base mesh → list of follower bindings
            // (remap is null for 1:1 sync, else maps source weight → follower weight)
            var followers = new Dictionary<string, List<(string path, string shape, AnimationCurve remap)>>();

            foreach (var sync in syncs)
            {
                if (sync.blendshapes == null) continue;

                foreach (var entry in sync.blendshapes)
                {
                    if (entry.localMesh == null || entry.targetMesh == null) continue;
                    if (string.IsNullOrEmpty(entry.localBlendshape) ||
                        string.IsNullOrEmpty(entry.targetBlendshape)) continue;

                    var sourcePath   = GetPath(avatarRoot.transform, entry.targetMesh.transform);
                    var followerPath = GetPath(avatarRoot.transform, entry.localMesh.transform);
                    if (sourcePath == null || followerPath == null) continue;

                    var remap = entry.useCurve ? entry.curve : null;

                    // Copy the current static weight so the un-animated state matches.
                    CopyStaticWeight(entry.targetMesh, entry.targetBlendshape,
                                     entry.localMesh,  entry.localBlendshape, remap);

                    var key = $"{sourcePath}|{entry.targetBlendshape}";
                    if (!followers.TryGetValue(key, out var list))
                        followers[key] = list = new List<(string, string, AnimationCurve)>();
                    list.Add((followerPath, entry.localBlendshape, remap));
                }

                Object.DestroyImmediate(sync);
            }

            if (followers.Count == 0) return;

            EnsureTempFolder();
            int retargeted = 0;

            // 1. Retarget clips inside the merged animator controller.
            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            var controller = avatar?.overrides?.runtimeAnimatorController as AnimatorController;
            if (controller != null)
                retargeted += RetargetController(controller, followers);

            // 2. Retarget AAS Toggle on/off clips (ShapeChanger / ObjectToggle / etc.).
            if (avatar?.avatarSettings?.settings != null)
            {
                foreach (var setting in avatar.avatarSettings.settings)
                {
                    var ts = setting?.toggleSettings;
                    if (ts == null || !ts.useAnimationClip) continue;

                    if (ts.animationClip != null &&
                        TryRetarget(ts.animationClip, followers, out var newOn))
                    { ts.animationClip = newOn; retargeted++; }

                    if (ts.offAnimationClip != null &&
                        TryRetarget(ts.offAnimationClip, followers, out var newOff))
                    { ts.offAnimationClip = newOff; retargeted++; }
                }
            }

            if (retargeted > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[MA-CVR] BlendshapeSync: retargeted {retargeted} animation clip(s) " +
                          $"covering {followers.Count} followed blendshape(s).");
            }
        }

        // ------------------------------------------------------------------ controller walk

        private static int RetargetController(
            AnimatorController controller,
            Dictionary<string, List<(string path, string shape, AnimationCurve remap)>> followers)
        {
            int count = 0;
            // Map original clip → retargeted copy, so a clip shared between states
            // is only processed once and all references swap to the same copy.
            var replaced = new Dictionary<AnimationClip, AnimationClip>();

            foreach (var layer in controller.layers)
                count += RetargetStateMachine(layer.stateMachine, followers, replaced);

            return count;
        }

        private static int RetargetStateMachine(
            AnimatorStateMachine sm,
            Dictionary<string, List<(string path, string shape, AnimationCurve remap)>> followers,
            Dictionary<AnimationClip, AnimationClip> replaced)
        {
            int count = 0;

            foreach (var child in sm.states)
            {
                var motion = RetargetMotion(child.state.motion, followers, replaced, ref count);
                if (!ReferenceEquals(motion, child.state.motion))
                    child.state.motion = motion;
            }

            foreach (var sub in sm.stateMachines)
                count += RetargetStateMachine(sub.stateMachine, followers, replaced);

            return count;
        }

        private static Motion RetargetMotion(
            Motion motion,
            Dictionary<string, List<(string path, string shape, AnimationCurve remap)>> followers,
            Dictionary<AnimationClip, AnimationClip> replaced,
            ref int count)
        {
            switch (motion)
            {
                case AnimationClip clip:
                    if (replaced.TryGetValue(clip, out var cached)) return cached;
                    if (TryRetarget(clip, followers, out var copy))
                    {
                        replaced[clip] = copy;
                        count++;
                        return copy;
                    }
                    replaced[clip] = clip;
                    return clip;

                case BlendTree tree:
                    var children = tree.children;
                    for (int i = 0; i < children.Length; i++)
                        children[i].motion = RetargetMotion(children[i].motion, followers, replaced, ref count);
                    tree.children = children;
                    return tree;

                default:
                    return motion;
            }
        }

        // ------------------------------------------------------------------ clip retarget

        /// <summary>
        /// If the clip animates any followed blendshape, produces a CLONE with extra
        /// curves added for every follower binding and returns true. The original
        /// asset is never modified.
        /// </summary>
        private static bool TryRetarget(
            AnimationClip source,
            Dictionary<string, List<(string path, string shape, AnimationCurve remap)>> followers,
            out AnimationClip result)
        {
            result = null;
            List<(EditorCurveBinding srcBinding, List<(string path, string shape, AnimationCurve remap)> dests)> hits = null;

            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.type != typeof(SkinnedMeshRenderer)) continue;
                if (!binding.propertyName.StartsWith("blendShape.")) continue;

                var shapeName = binding.propertyName.Substring("blendShape.".Length);
                if (followers.TryGetValue($"{binding.path}|{shapeName}", out var dests))
                {
                    hits ??= new List<(EditorCurveBinding, List<(string, string, AnimationCurve)>)>();
                    hits.Add((binding, dests));
                }
            }

            if (hits == null) return false;

            var copy = Object.Instantiate(source);
            copy.name = source.name + "_BSSync";

            foreach (var (srcBinding, dests) in hits)
            {
                var curve = AnimationUtility.GetEditorCurve(source, srcBinding);
                foreach (var (path, shape, remap) in dests)
                {
                    var followerCurve = remap == null ? curve : RemapCurve(curve, remap);
                    copy.SetCurve(path, typeof(SkinnedMeshRenderer), $"blendShape.{shape}", followerCurve);
                }
            }

            EnsureTempFolder();
            var assetPath = $"{TempFolder}/MA_CVR_{copy.name}_{GUID.Generate()}.anim";
            AssetDatabase.CreateAsset(copy, assetPath);
            result = copy;
            return true;
        }

        /// <summary>
        /// Transforms an animation curve's VALUES through the remap curve (weight → weight),
        /// keeping keyframe times. Tangents follow the chain rule; instant (Infinity)
        /// tangents are preserved so stepped toggle clips stay stepped.
        /// </summary>
        private static AnimationCurve RemapCurve(AnimationCurve source, AnimationCurve remap)
        {
            const float epsilon = 0.01f;
            var keys = source.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                float slope = (remap.Evaluate(k.value + epsilon) - remap.Evaluate(k.value - epsilon))
                              / (2f * epsilon);
                k.value = remap.Evaluate(k.value);
                if (!float.IsInfinity(k.inTangent))  k.inTangent  *= slope;
                if (!float.IsInfinity(k.outTangent)) k.outTangent *= slope;
                keys[i] = k;
            }
            return new AnimationCurve(keys)
            {
                preWrapMode  = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
        }

        private static void CopyStaticWeight(
            SkinnedMeshRenderer sourceSmr, string sourceShape,
            SkinnedMeshRenderer destSmr, string destShape, AnimationCurve remap)
        {
            if (sourceSmr.sharedMesh == null || destSmr.sharedMesh == null) return;

            int srcIdx = sourceSmr.sharedMesh.GetBlendShapeIndex(sourceShape);
            int dstIdx = destSmr.sharedMesh.GetBlendShapeIndex(destShape);
            if (srcIdx < 0)
            {
                Debug.LogWarning($"[MA-CVR] BlendshapeSync: blendshape '{sourceShape}' not found on '{sourceSmr.name}'.");
                return;
            }
            if (dstIdx < 0)
            {
                Debug.LogWarning($"[MA-CVR] BlendshapeSync: blendshape '{destShape}' not found on '{destSmr.name}'.");
                return;
            }

            float weight = sourceSmr.GetBlendShapeWeight(srcIdx);
            destSmr.SetBlendShapeWeight(dstIdx, remap != null ? remap.Evaluate(weight) : weight);
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
