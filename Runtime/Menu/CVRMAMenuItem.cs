using System;
using UnityEngine;

namespace ModularAvatarCVR
{
    /// <summary>
    /// Maps to a CVR Advanced Avatar Setting entry type.
    /// </summary>
    public enum CVRMAControlType
    {
        /// <summary>Bool/float/int toggle → CVR AAS Toggle</summary>
        Toggle = 0,
        /// <summary>Momentary press → CVR AAS Toggle (treated as brief on-state)</summary>
        Button = 1,
        /// <summary>Contains child menu items; children are flattened into AAS at build time.</summary>
        SubMenu = 2,
        /// <summary>Single float parameter → CVR AAS Slider</summary>
        RadialPuppet = 3,
        /// <summary>Two float parameters → CVR AAS Joystick2D</summary>
        TwoAxisPuppet = 4,
        /// <summary>Two float pairs → CVR AAS Joystick2D (best approximation; extra axes discarded)</summary>
        FourAxisPuppet = 5,
    }

    /// <summary>
    /// Animator parameter type driven by a Toggle/Button menu item (mirrors VRC MA).
    /// </summary>
    public enum CVRMAMenuParameterType
    {
        /// <summary>Standard on/off toggle → CVR AAS Toggle (Bool).</summary>
        Bool = 0,
        /// <summary>Set an Int parameter to Value when active. All Int toggles sharing a
        /// parameter become ONE AAS Dropdown — exclusive selection, like VRC MA radio toggles.</summary>
        Int = 1,
        /// <summary>Float parameter written as 0/1 by a CVR AAS Toggle.</summary>
        Float = 2,
    }

    /// <summary>
    /// Defines a single control in the avatar's Advanced Settings menu.
    /// Place inside a CVRMAMenuInstaller hierarchy to be installed at build time.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Menu Item")]
    public class CVRMAMenuItem : CVRMAComponent
    {
        [Tooltip("Display name in the CVR Quick Menu. Defaults to the GameObject name.")]
        public string label = "";

        public CVRMAControlType controlType = CVRMAControlType.Toggle;

        [Tooltip("Primary animator parameter name (for Toggle, Button, RadialPuppet).")]
        public string parameter = "";

        [Tooltip("Parameter type for Toggle/Button. Int toggles sharing a parameter become one AAS Dropdown.")]
        public CVRMAMenuParameterType parameterType = CVRMAMenuParameterType.Bool;

        [Tooltip("Value written to the parameter when this control is active (Int/Float toggles).")]
        public float value = 1f;

        [Tooltip("Default value when the avatar is loaded.")]
        public float defaultValue = 0f;

        [Tooltip("For TwoAxisPuppet/FourAxisPuppet: base name; CVR appends -x/-y suffixes.")]
        public string joystickBaseName = "";

        [Tooltip("If true, parameter is network synced.")]
        public bool isSynced = true;

        [Tooltip("If true, parameter value is saved across avatar changes.")]
        public bool isSaved = true;

        public string GetEffectiveLabel() =>
            string.IsNullOrEmpty(label) ? gameObject.name : label;

        public string GetEffectiveMachineName() =>
            string.IsNullOrEmpty(parameter) ? gameObject.name.ToLower().Replace(" ", "_") : parameter;
    }
}
