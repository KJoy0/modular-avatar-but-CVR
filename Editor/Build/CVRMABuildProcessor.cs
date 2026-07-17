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
    ///   1. ReplaceObject        — swap objects into their final hierarchy positions
    ///   2. VisibleHeadAccessory — add FPRExclusion to head accessories
    ///   3. BoneProxy            — reparent objects to target bones
    ///   4. MergeArmature        — merge accessory armatures into avatar armature
    ///   5. ScaleAdjuster        — scale bones with child compensation
    ///   6. FloorAdjuster        — raise avatar so the marked height becomes the floor
    ///   7. MeshSettings         — apply probe anchor / bounds to renderers
    ///   8. RemoveVertexColors   — strip vertex colors (cloned meshes)
    ///   9. MeshCutter           — delete or split-to-toggle mesh portions (cloned meshes;
    ///                             Toggle mode emits CVRMAObjectToggles, so must precede ObjectToggle)
    ///  10. ShapeChanger         — AAS Toggle + animation clips for blendshape changes
    ///  11. Parameters           — AAS entries for synced parameters
    ///  12. ObjectToggle         — AAS Toggle + animator layer for GameObject on/off
    ///  13. MaterialSwap         — AAS Toggle + animation clips for material swaps
    ///  14. MaterialSetter       — AAS Toggle + animation clips for material overrides
    ///  15. ModularSettings      — install reusable AAS entry bundles
    ///  16. MenuToAAS            — convert menu items to Advanced Avatar Settings entries
    ///  17. MergeBlendTree       — wrap blend trees for animator merge
    ///  18. MergeAnimator        — merge animator controllers into avatar override controller
    ///  19. BlendshapeSync       — retarget blendshape curves in the merged controller (last)
    /// </summary>
    public class CVRMABuildProcessor : CCKBuildProcessor
    {
        public override int CallbackOrder => -100; // run before most other processors

        public override void OnPreProcessAvatar(GameObject avatar)
        {
            if (avatar == null) return;
            if (!HasAnyMAComponent(avatar)) return;

            Debug.Log($"[MA-CVR] Processing avatar: {avatar.name}");

            CVRMAReplaceObjectPass.Run(avatar);    // first — later passes see the final hierarchy
            CVRMAVisibleHeadAccessoryPass.Run(avatar);
            CVRMABoneProxyPass.Run(avatar);
            CVRMAMergeArmaturePass.Run(avatar);
            CVRMAScaleAdjusterPass.Run(avatar);    // after merge — bones are in their final place
            CVRMAFloorAdjusterPass.Run(avatar);    // after merge/scale — bone heights are final
            CVRMAMeshSettingsPass.Run(avatar);
            CVRMARemoveVertexColorsPass.Run(avatar);
            CVRMAMeshCutterPass.Run(avatar);       // Toggle mode emits ObjectToggles — must precede that pass
            CVRMAShapeChangerPass.Run(avatar);
            CVRMAParametersPass.Run(avatar);
            CVRMAObjectTogglePass.Run(avatar);
            CVRMAMaterialSwapPass.Run(avatar);
            CVRMAMaterialSetterPass.Run(avatar);
            CVRMAModularSettingsInstallerPass.Run(avatar);
            CVRMAMenuToAASPass.Run(avatar);
            CVRMAMergeBlendTreePass.Run(avatar);   // must run before animator merge
            CVRMAMergeAnimatorPass.Run(avatar);
            CVRMABlendshapeSyncPass.Run(avatar);   // retargets clips in the MERGED controller — must run last
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
