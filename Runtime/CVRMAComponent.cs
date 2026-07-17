using UnityEngine;

namespace ModularAvatarCVR
{
    /// <summary>
    /// Base class for all Modular Avatar CVR components.
    /// Components are editor-only markers processed before the CCK build.
    /// </summary>
    [ExecuteInEditMode]
    public abstract class CVRMAComponent : MonoBehaviour
    {
        protected virtual void OnValidate() { }

        /// <summary>
        /// Resolves the parameter for a reactive component (ObjectToggle, ShapeChanger,
        /// MaterialSwap, MaterialSetter), mirroring VRC MA's reaction model:
        ///   1. an explicitly set parameter wins;
        ///   2. otherwise inherit the nearest parent (or own) MA Menu Item's machine name;
        ///   3. otherwise fall back to a machine-name version of the GameObject's name.
        /// </summary>
        protected string ResolveReactiveParameter(string explicitParameter)
        {
            if (!string.IsNullOrEmpty(explicitParameter)) return explicitParameter;

            var menuItem = GetComponentInParent<CVRMAMenuItem>(true);
            if (menuItem != null) return menuItem.GetEffectiveMachineName();

            return gameObject.name.ToLower().Replace(" ", "_");
        }
    }
}
