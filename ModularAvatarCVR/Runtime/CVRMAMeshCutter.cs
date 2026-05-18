using UnityEngine;

namespace ModularAvatarCVR
{
    public enum CVRMAMeshCutterMode
    {
        /// <summary>Keep vertices that appear in ANY active cutter region (union).</summary>
        VertexUnion,
        /// <summary>Keep only vertices that appear in ALL active cutter regions (intersection).</summary>
        VertexIntersection
    }

    /// <summary>
    /// Marker component preserving MA Mesh Cutter data after conversion.
    ///
    /// CVR does not have a native vertex-cutter system. At build time this
    /// component logs a warning and is removed. Use it as a reference for
    /// manual reproduction via blendshapes or separate meshes.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Mesh Cutter")]
    public class CVRMAMeshCutter : CVRMAComponent
    {
        [Tooltip("The mesh whose vertices will be cut (stored for reference only).")]
        public SkinnedMeshRenderer targetMesh;

        public CVRMAMeshCutterMode mode = CVRMAMeshCutterMode.VertexUnion;
    }
}
