using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR
{
    [Serializable]
    public class CVRMAToggledObject
    {
        [Tooltip("Object to show or hide when the parameter is active.")]
        public Transform target;
        [Tooltip("Should the object be active (true) or inactive (false) when the parameter is ON?")]
        public bool activeWhenOn = true;
    }

    /// <summary>
    /// Toggles a list of GameObjects based on a boolean animator parameter.
    ///
    /// Unlike other MA-CVR components this one has an inspector button
    /// ("Apply to AAS") that writes the entry to the avatar's Advanced Avatar
    /// Settings immediately at edit time, so you can preview it in the CCK inspector
    /// without uploading. The build processor also applies it as a safety net.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Object Toggle")]
    public class CVRMAObjectToggle : CVRMAComponent
    {
        [Tooltip("Display name shown in the CVR Quick Menu. Defaults to the GameObject name.")]
        public string label = "";

        [Tooltip("Animator parameter name. If empty, inherits the nearest parent MA Menu Item's parameter, else the GameObject name.")]
        public string parameter = "";

        [Tooltip("Default state when the avatar loads.")]
        public bool defaultValue = false;

        public List<CVRMAToggledObject> objects = new List<CVRMAToggledObject>();

        public string GetEffectiveLabel()     => string.IsNullOrEmpty(label)     ? gameObject.name : label;
        public string GetEffectiveParameter() => ResolveReactiveParameter(parameter);
    }
}
