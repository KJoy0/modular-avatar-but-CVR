#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Applies CVRMAScaleAdjuster: multiplies the bone's localScale while
    /// compensating each direct child's localPosition and localScale so the
    /// rest of the hierarchy stays visually unchanged. Only geometry skinned
    /// to the adjusted bone itself changes size.
    /// </summary>
    internal static class CVRMAScaleAdjusterPass
    {
        internal static void Run(GameObject avatarRoot)
        {
            // Deepest-first so nested adjusters compose predictably.
            var adjusters = new List<CVRMAScaleAdjuster>(
                avatarRoot.GetComponentsInChildren<CVRMAScaleAdjuster>(true));
            adjusters.Sort((a, b) => Depth(b.transform).CompareTo(Depth(a.transform)));

            foreach (var adjuster in adjusters)
            {
                Apply(adjuster);
                Object.DestroyImmediate(adjuster);
            }
        }

        private static void Apply(CVRMAScaleAdjuster adjuster)
        {
            var s = adjuster.scale;
            if (s == Vector3.one) return;

            if (Mathf.Approximately(s.x, 0f) || Mathf.Approximately(s.y, 0f) || Mathf.Approximately(s.z, 0f))
            {
                Debug.LogWarning(
                    $"[MA-CVR] ScaleAdjuster on '{adjuster.gameObject.name}': zero scale component — skipped.");
                return;
            }

            var t = adjuster.transform;
            var inverse = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);

            t.localScale = Vector3.Scale(t.localScale, s);

            // Compensate children so they keep their world pose and size.
            foreach (Transform child in t)
            {
                child.localPosition = Vector3.Scale(child.localPosition, inverse);
                child.localScale    = Vector3.Scale(child.localScale,    inverse);
            }
        }

        private static int Depth(Transform t)
        {
            int d = 0;
            while (t.parent != null) { d++; t = t.parent; }
            return d;
        }
    }
}
#endif
