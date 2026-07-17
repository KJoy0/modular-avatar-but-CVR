#if UNITY_EDITOR
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Applies CVRMAMeshSettings to all SkinnedMeshRenderers in each component's subtree.
    /// Outer settings are overridden by inner settings (inner wins).
    /// </summary>
    internal static class CVRMAMeshSettingsPass
    {
        internal static void Run(GameObject avatarRoot)
        {
            var components = avatarRoot.GetComponentsInChildren<CVRMAMeshSettings>(true);
            if (components.Length == 0) return;

            foreach (var settings in components)
            {
                ApplyToSubtree(settings);
                Object.DestroyImmediate(settings);
            }
        }

        private static void ApplyToSubtree(CVRMAMeshSettings settings)
        {
            foreach (var smr in settings.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Skip if a closer (inner) settings component already owns this renderer
                var closerSettings = FindClosestSettings(smr.transform, settings.transform);
                if (closerSettings != null && closerSettings != settings) continue;

                ApplyProbeAnchor(settings, smr);
                ApplyBounds(settings, smr);
            }
        }

        private static void ApplyProbeAnchor(CVRMAMeshSettings settings, SkinnedMeshRenderer smr)
        {
            switch (settings.inheritProbeAnchor)
            {
                case CVRMAInheritMode.Set:
                case CVRMAInheritMode.SetOrInherit:
                    if (settings.probeAnchor != null)
                        smr.probeAnchor = settings.probeAnchor;
                    break;
            }
        }

        private static void ApplyBounds(CVRMAMeshSettings settings, SkinnedMeshRenderer smr)
        {
            switch (settings.inheritBounds)
            {
                case CVRMAInheritMode.Set:
                case CVRMAInheritMode.SetOrInherit:
                    if (settings.rootBone != null)
                        smr.rootBone = settings.rootBone;
                    smr.localBounds = settings.localBounds;
                    break;
            }
        }

        private static CVRMAMeshSettings FindClosestSettings(Transform rendererTransform, Transform stopAt)
        {
            var current = rendererTransform;
            while (current != null && current != stopAt)
            {
                var s = current.GetComponent<CVRMAMeshSettings>();
                if (s != null) return s;
                current = current.parent;
            }
            return null;
        }
    }
}
#endif
