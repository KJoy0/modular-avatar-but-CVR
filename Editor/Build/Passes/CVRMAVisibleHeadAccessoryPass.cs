#if UNITY_EDITOR
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// For each CVRMAVisibleHeadAccessory, adds an FPRExclusion component with
    /// isShown = true so CVR's TransformHider keeps the object visible in first-person.
    /// </summary>
    internal static class CVRMAVisibleHeadAccessoryPass
    {
        internal static void Run(GameObject avatarRoot)
        {
            var accessories = avatarRoot.GetComponentsInChildren<CVRMAVisibleHeadAccessory>(true);
            if (accessories.Length == 0) return;

            foreach (var acc in accessories)
            {
                Apply(acc.gameObject, acc.shrinkToZero);

                if (acc.includeChildren)
                    foreach (Transform child in acc.transform.GetComponentsInChildren<Transform>(true))
                        if (child.gameObject != acc.gameObject)
                            Apply(child.gameObject, acc.shrinkToZero);

                Object.DestroyImmediate(acc);
            }
        }

        private static void Apply(GameObject go, bool shrinkToZero)
        {
            // Only add FPRExclusion if not already present
            var existing = go.GetComponent<ABI.CCK.Components.FPRExclusion>();
            if (existing != null)
            {
                existing.isShown = true;
                return;
            }

            var fpr = go.AddComponent<ABI.CCK.Components.FPRExclusion>();
            fpr.isShown      = true;
            fpr.target       = go.transform;
            fpr.shrinkToZero = shrinkToZero;
        }
    }
}
#endif
