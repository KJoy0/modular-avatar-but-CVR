using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR
{
    [Serializable]
    public class CVRMABlendshapeSyncEntry
    {
        [Tooltip("Mesh containing the source blendshape.")]
        public SkinnedMeshRenderer localMesh;
        [Tooltip("Name of the blendshape on the local mesh.")]
        public string localBlendshape;
        [Tooltip("Mesh containing the target blendshape to sync to.")]
        public SkinnedMeshRenderer targetMesh;
        [Tooltip("Name of the blendshape on the target mesh.")]
        public string targetBlendshape;
    }

    /// <summary>
    /// Synchronizes blendshapes between meshes via a generated animator layer at build time.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Blendshape Sync")]
    public class CVRMABlendshapeSync : CVRMAComponent
    {
        public List<CVRMABlendshapeSyncEntry> blendshapes = new List<CVRMABlendshapeSyncEntry>();
    }
}
