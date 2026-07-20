using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR
{
    [Serializable]
    public enum CVRMAShapeChangeType
    {
        /// <summary>Set the blendshape to Value when the parameter is active.</summary>
        Set,
        /// <summary>
        /// Delete the polygons displaced by this blendshape when the parameter is
        /// active (mirrors VRC MA). Implemented as a generated collapse blendshape
        /// on a cloned mesh, so it toggles at runtime. For permanent removal use
        /// Mesh Cutter with a By Blendshape filter instead.
        /// </summary>
        Delete
    }

    [Serializable]
    public class CVRMAChangedShape
    {
        [Tooltip("SkinnedMeshRenderer that owns the blendshape.")]
        public SkinnedMeshRenderer targetMesh;
        [BlendshapeName(nameof(targetMesh))]
        public string shapeName;
        public CVRMAShapeChangeType changeType = CVRMAShapeChangeType.Set;
        [Range(0f, 100f)] public float value = 100f;
    }

    /// <summary>
    /// When the controlling parameter is active, sets blendshapes or deletes the
    /// polygons a blendshape displaces (VRC MA Shape Changer behavior). Generates
    /// an AAS toggle + animator layer at build time.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Shape Changer")]
    public class CVRMAShapeChanger : CVRMAComponent
    {
        [Tooltip("Animator parameter that activates these shape changes. Defaults to the GameObject name.")]
        public string parameter = "";

        [Tooltip("Apply the changes when the parameter is INACTIVE instead of active.")]
        public bool inverseCondition = false;

        [Tooltip("Parameter must exceed this value to be considered active.")]
        public float threshold = 0.5f;

        [Tooltip("Minimum vertex displacement (in meters, at full weight) for a polygon to count as affected by a Delete shape.")]
        public float displacementThreshold = 0.01f;

        [Tooltip("Default state of the parameter (off = false).")]
        public bool defaultValue = false;

        public List<CVRMAChangedShape> shapes = new List<CVRMAChangedShape>();

        public string GetEffectiveParameter() => ResolveReactiveParameter(parameter);
    }
}
