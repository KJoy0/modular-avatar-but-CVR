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

        [Tooltip("Optional: toggle this component's enabled state instead of the whole GameObject.")]
        public Component component;

        [Tooltip("Should the target be active/enabled (true) or inactive/disabled (false) when the parameter is ON?")]
        public bool activeWhenOn = true;

        /// <summary>True when this entry drives a component's enabled state rather than a GameObject.</summary>
        public bool TogglesComponent => component != null;

        /// <summary>The transform whose animation path addresses this entry.</summary>
        public Transform TargetTransform => component != null ? component.transform : target;

        /// <summary>What the inspector shows in the object field: the component, else the GameObject.</summary>
        public UnityEngine.Object EffectiveTarget =>
            component != null ? component : (target != null ? (UnityEngine.Object)target.gameObject : null);

        /// <summary>
        /// Assigns either a GameObject or a Component from a single object field,
        /// keeping <see cref="target"/> pointed at the owning transform either way.
        /// </summary>
        public void SetTarget(UnityEngine.Object value)
        {
            switch (value)
            {
                case GameObject go:
                    target = go.transform;
                    component = null;
                    break;
                case Component c:
                    component = c;
                    target = c.transform;
                    break;
                default:
                    target = null;
                    component = null;
                    break;
            }
        }

        /// <summary>
        /// Components Unity can enable/disable (they expose an `m_Enabled` property that
        /// the animation system can bind). Transforms and plain Components cannot.
        /// </summary>
        public static bool CanToggle(Component c) =>
            c is Behaviour || c is Renderer || c is Collider || c is LODGroup;

        public static bool GetEnabled(Component c)
        {
            switch (c)
            {
                case Behaviour b: return b.enabled;
                case Renderer r:  return r.enabled;
                case Collider co: return co.enabled;
                case LODGroup l:  return l.enabled;
                default:          return true;
            }
        }

        public static void SetEnabled(Component c, bool value)
        {
            switch (c)
            {
                case Behaviour b: b.enabled = value;  break;
                case Renderer r:  r.enabled = value;  break;
                case Collider co: co.enabled = value; break;
                case LODGroup l:  l.enabled = value;  break;
            }
        }
    }

    /// <summary>
    /// Toggles GameObjects and/or individual components based on an animator parameter.
    ///
    /// Each entry targets either a GameObject (its active state is animated) or a single
    /// component (its enabled state is animated) — useful for switching off Magica Cloth,
    /// colliders, audio, particles or constraints without hiding the object that owns them.
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
