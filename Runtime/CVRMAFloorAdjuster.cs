using UnityEngine;

namespace ModularAvatarCVR
{
    /// <summary>
    /// Marks the desired floor level for the avatar, mirroring VRC MA's Floor Adjuster.
    ///
    /// Position this GameObject vertically at the bottom of the avatar's shoes. At
    /// build time the avatar's skeleton (Hips) is raised so the marked level aligns
    /// with the avatar root — keeping tall shoes/heels from sinking into the floor.
    /// The CVRAvatar view and voice positions are raised by the same amount.
    ///
    /// If more than one Floor Adjuster is present on the avatar, no adjustment is
    /// made (the floor height cannot change per-outfit at runtime).
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Floor Adjuster")]
    [DisallowMultipleComponent]
    public class CVRMAFloorAdjuster : CVRMAComponent
    {
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Floor plane indicator at this object's height.
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            var p = transform.position;
            Gizmos.DrawCube(new Vector3(p.x, p.y, p.z), new Vector3(0.5f, 0.001f, 0.5f));
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
            Gizmos.DrawLine(p - Vector3.right * 0.25f, p + Vector3.right * 0.25f);
            Gizmos.DrawLine(p - Vector3.forward * 0.25f, p + Vector3.forward * 0.25f);
        }
#endif
    }
}
