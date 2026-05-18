#if UNITY_EDITOR
using CVR.CCKEditor.ContentBuilder;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// CVR CCK build processor for Modular Avatar CVR.
    /// Automatically discovered by CCKBuildProcessorManager via reflection.
    ///
    /// Pass execution order for avatars:
    ///   1. BoneProxy       — reparent objects to target bones
    ///   2. MergeArmature   — merge accessory armatures into avatar armature
    ///   3. MeshSettings    — apply probe anchor / bounds to renderers
    ///   4. BlendshapeSync  — inject blendshape-sync animator layers
    ///   5. ShapeChanger    — AAS Toggle + animation clips for blendshape changes
    ///   6. Parameters      — AAS entries for synced parameters
    ///   7. ObjectToggle    — AAS Toggle entries for GameObject on/off
    ///   8. MaterialSwap    — AAS Toggle + animation clips for material swaps
    ///   9. MaterialSetter  — AAS Toggle + animation clips for material overrides
    ///  10. MeshCutter      — warn and remove (no CVR equivalent)
    ///  11. MenuToAAS       — convert menu items to Advanced Avatar Settings entries
    ///  12. MergeBlendTree  — wrap blend trees for animator merge
    ///  13. MergeAnimator   — merge animator controllers into avatar override controller
    /// </summary>
    public class CVRMABuildProcessor : CCKBuildProcessor
    {
        public override int CallbackOrder => -100; // run before most other processors

        public override void OnPreProcessAvatar(GameObject avatar)
        {
            if (avatar == null) return;
            if (!HasAnyMAComponent(avatar)) return;

            Debug.Log($"[MA-CVR] Processing avatar: {avatar.name}");

            CVRMABoneProxyPass.Run(avatar);
            CVRMAMergeArmaturePass.Run(avatar);
            CVRMAMeshSettingsPass.Run(avatar);
            CVRMABlendshapeSyncPass.Run(avatar);
            CVRMAShapeChangerPass.Run(avatar);
            CVRMAParametersPass.Run(avatar);
            CVRMAObjectTogglePass.Run(avatar);
            CVRMAMaterialSwapPass.Run(avatar);
            CVRMAMaterialSetterPass.Run(avatar);
            RemoveMeshCutters(avatar);
            CVRMAMenuToAASPass.Run(avatar);
            CVRMAMergeBlendTreePass.Run(avatar);   // must run before animator merge
            CVRMAMergeAnimatorPass.Run(avatar);
        }

        private static void RemoveMeshCutters(GameObject avatar)
        {
            foreach (var cutter in avatar.GetComponentsInChildren<CVRMAMeshCutter>(true))
            {
                Debug.LogWarning(
                    $"[MA-CVR] MeshCutter on '{cutter.gameObject.name}' cannot be converted — " +
                    "CVR has no native vertex-cutter system. Reproduce manually via blendshapes or separate meshes.");
                Object.DestroyImmediate(cutter);
            }
        }

        public override void OnPostProcessAvatar()
        {
            CVRMAMergeAnimatorPass.Cleanup();
        }

        private static bool HasAnyMAComponent(GameObject avatar)
        {
            return avatar.GetComponentInChildren<CVRMAComponent>(true) != null;
        }
    }
}
#endif
