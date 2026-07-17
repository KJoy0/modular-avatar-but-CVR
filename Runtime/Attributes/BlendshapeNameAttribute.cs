using UnityEngine;

namespace ModularAvatarCVR
{
    /// <summary>
    /// Marks a string field as a blendshape name. When drawn by the
    /// matching PropertyDrawer it shows a dropdown of all blendshapes
    /// found on the SkinnedMeshRenderer in the sibling field.
    /// </summary>
    public class BlendshapeNameAttribute : PropertyAttribute
    {
        /// <summary>Name of the sibling field holding the SkinnedMeshRenderer to pull blendshapes from.</summary>
        public readonly string meshFieldName;

        public BlendshapeNameAttribute(string meshFieldName)
        {
            this.meshFieldName = meshFieldName;
        }
    }
}
