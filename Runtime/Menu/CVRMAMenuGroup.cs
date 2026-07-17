using UnityEngine;

namespace ModularAvatarCVR
{
    /// <summary>
    /// Groups child CVRMAMenuItem components together.
    /// Since CVR AAS is a flat list, the group's children are all added at the same level.
    /// Optionally, a name prefix can be added to all children for organisation.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Menu Group")]
    public class CVRMAMenuGroup : CVRMAComponent
    {
        [Tooltip("Optional object to source child menu items from instead of this object's children.")]
        public GameObject sourceObject;

        public GameObject EffectiveSourceObject => sourceObject != null ? sourceObject : gameObject;
    }
}
