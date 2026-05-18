using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR
{
    [Serializable]
    public class CVRMAMatSwap
    {
        [Tooltip("Material to replace.")]
        public Material from;
        [Tooltip("Material to use when the toggle is ON.")]
        public Material to;
    }

    /// <summary>
    /// When the toggle parameter is ON, swaps materials across all renderers
    /// under the root object. Generates AAS Toggle + animation clips at build time.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Material Swap")]
    public class CVRMAMaterialSwap : CVRMAComponent
    {
        [Tooltip("Display name in the CVR Quick Menu.")]
        public string label = "";

        [Tooltip("Animator parameter name.")]
        public string parameter = "";

        [Tooltip("Root object whose renderers are searched for materials to swap. Defaults to avatar root.")]
        public Transform swapRoot;

        public bool defaultValue = false;

        public List<CVRMAMatSwap> swaps = new List<CVRMAMatSwap>();

        public string GetEffectiveLabel()     => string.IsNullOrEmpty(label)     ? gameObject.name : label;
        public string GetEffectiveParameter() => string.IsNullOrEmpty(parameter) ? gameObject.name.ToLower().Replace(" ", "_") : parameter;
    }
}
