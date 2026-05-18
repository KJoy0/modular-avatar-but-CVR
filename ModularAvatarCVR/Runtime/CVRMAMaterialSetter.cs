using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR
{
    [Serializable]
    public class CVRMAMaterialSetEntry
    {
        [Tooltip("Renderer to target.")]
        public Renderer targetRenderer;
        [Tooltip("Material slot index to replace.")]
        public int materialIndex;
        [Tooltip("Material to apply when the toggle is ON.")]
        public Material material;
    }

    /// <summary>
    /// Sets specific material slots on renderers when the toggle parameter is ON.
    /// Generates AAS Toggle + animation clips at build time.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Material Setter")]
    public class CVRMAMaterialSetter : CVRMAComponent
    {
        [Tooltip("Display name in the CVR Quick Menu.")]
        public string label = "";

        [Tooltip("Animator parameter name.")]
        public string parameter = "";

        public bool defaultValue = false;

        public List<CVRMAMaterialSetEntry> entries = new List<CVRMAMaterialSetEntry>();

        public string GetEffectiveLabel()     => string.IsNullOrEmpty(label)     ? gameObject.name : label;
        public string GetEffectiveParameter() => string.IsNullOrEmpty(parameter) ? gameObject.name.ToLower().Replace(" ", "_") : parameter;
    }
}
