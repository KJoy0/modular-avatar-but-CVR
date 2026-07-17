#if UNITY_EDITOR
using ABI.CCK.Components;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Raises the avatar so the Floor Adjuster's height becomes the floor: the Hips
    /// bone is moved up by (avatar root Y − adjuster Y), and the CVRAvatar view/voice
    /// positions follow. Mirrors VRC MA's Floor Adjuster, including its rule that
    /// multiple adjusters cancel the adjustment entirely.
    ///
    /// Must run after MergeArmature/ScaleAdjuster so bone heights are final.
    /// </summary>
    internal static class CVRMAFloorAdjusterPass
    {
        internal static void Run(GameObject avatarRoot)
        {
            var adjusters = avatarRoot.GetComponentsInChildren<CVRMAFloorAdjuster>(true);
            if (adjusters.Length == 0) return;

            try
            {
                if (adjusters.Length > 1)
                {
                    Debug.LogWarning(
                        $"[MA-CVR] FloorAdjuster: {adjusters.Length} adjusters found — the floor height " +
                        "can only be set once per avatar, so NO adjustment was applied. Remove all but one.");
                    return;
                }

                Apply(avatarRoot, adjusters[0]);
            }
            finally
            {
                foreach (var adjuster in adjusters)
                {
                    // Remove bare marker objects entirely; otherwise just the component.
                    bool bareMarker = adjuster.transform.childCount == 0 &&
                                      adjuster.gameObject.GetComponents<Component>().Length <= 2;
                    Object.DestroyImmediate(bareMarker ? (Object)adjuster.gameObject : adjuster);
                }
            }
        }

        private static void Apply(GameObject avatarRoot, CVRMAFloorAdjuster adjuster)
        {
            var animator = avatarRoot.GetComponent<Animator>();
            var hips = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Hips)
                : null;
            if (hips == null)
            {
                Debug.LogWarning("[MA-CVR] FloorAdjuster: avatar has no humanoid Hips bone — skipped.");
                return;
            }

            float delta = avatarRoot.transform.position.y - adjuster.transform.position.y;
            if (Mathf.Approximately(delta, 0f)) return;

            hips.position += Vector3.up * delta;

            // Keep the view/voice points on the (now raised) head. CVRAvatar stores them
            // root-relative in world-scale units: TransformPoint(pos × inverseScale).
            var avatar = avatarRoot.GetComponent<CVRAvatar>();
            if (avatar != null)
            {
                var localDelta = Vector3.Scale(
                    avatarRoot.transform.InverseTransformVector(Vector3.up * delta),
                    avatarRoot.transform.lossyScale);
                avatar.viewPosition  += localDelta;
                avatar.voicePosition += localDelta;
            }

            Debug.Log($"[MA-CVR] FloorAdjuster: raised avatar by {delta:F4}m (hips, view and voice position).");
        }
    }
}
#endif
