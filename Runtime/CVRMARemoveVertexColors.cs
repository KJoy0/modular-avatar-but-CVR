using UnityEngine;

namespace ModularAvatarCVR
{
    public enum CVRMARemoveVertexColorMode
    {
        /// <summary>Strip vertex colors from all meshes in this subtree.</summary>
        Remove,
        /// <summary>Keep vertex colors — overrides a Remove component higher up the hierarchy.</summary>
        DontRemove
    }

    /// <summary>
    /// Strips vertex colors from all meshes in this object's subtree at build time
    /// (some shaders tint unexpectedly when vertex colors are present). A nested
    /// component with mode DontRemove excludes its own subtree.
    /// Meshes are cloned before modification — original assets are not touched.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Remove Vertex Colors")]
    public class CVRMARemoveVertexColors : CVRMAComponent
    {
        public CVRMARemoveVertexColorMode mode = CVRMARemoveVertexColorMode.Remove;
    }
}
