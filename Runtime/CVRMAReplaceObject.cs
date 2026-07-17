using UnityEngine;

namespace ModularAvatarCVR
{
    /// <summary>
    /// At build time, replaces another object with this one: the target is destroyed
    /// and this object takes its place in the hierarchy (same parent, same sibling
    /// index, same name). Used e.g. to ship an edited copy of the base body in a
    /// prefab without modifying the original avatar.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Replace Object")]
    [DisallowMultipleComponent]
    public class CVRMAReplaceObject : CVRMAComponent
    {
        [Tooltip("The object this one replaces at build time. It will be destroyed.")]
        public GameObject targetObject;
    }
}
