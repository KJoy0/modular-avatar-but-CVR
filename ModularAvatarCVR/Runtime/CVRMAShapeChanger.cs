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
        /// <summary>Delete (zero-out) the blendshape when the parameter is active.</summary>
        Delete
    }

    [Serializable]
    public class CVRMAChangedShape
    {
        [Tooltip("SkinnedMeshRenderer that owns the blendshape.")]
        public SkinnedMeshRenderer targetMesh;
        public string shapeName;
        public CVRMAShapeChangeType changeType = CVRMAShapeChangeType.Set;
        [Range(0f, 100f)] public float value = 100f;
    }

    /// <summary>
    /// When the controlling parameter is active (above threshold), sets or deletes
    /// blendshapes on target meshes. Generates a CVR AAS Toggle with ON/OFF
    /// animation clips at build time.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Shape Changer")]
    public class CVRMAShapeChanger : CVRMAComponent
    {
        [Tooltip("Animator parameter that activates these shape changes. Defaults to the GameObject name.")]
        public string parameter = "";

        [Tooltip("Parameter must exceed this value to be considered active.")]
        public float threshold = 0.5f;

        [Tooltip("Default state of the parameter (off = false).")]
        public bool defaultValue = false;

        public List<CVRMAChangedShape> shapes = new List<CVRMAChangedShape>();

        public string GetEffectiveParameter() =>
            string.IsNullOrEmpty(parameter)
                ? gameObject.name.ToLower().Replace(" ", "_")
                : parameter;
    }
}
