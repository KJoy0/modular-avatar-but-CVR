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
    }
}
