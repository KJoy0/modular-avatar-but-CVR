#if UNITY_EDITOR
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// For each CVRMAReplaceObject: moves this object to the target's place in
    /// the hierarchy (parent + sibling index + name) and destroys the target.
    /// </summary>
    internal static class CVRMAReplaceObjectPass
    {
        internal static void Run(GameObject avatarRoot)
        {
            foreach (var replacer in avatarRoot.GetComponentsInChildren<CVRMAReplaceObject>(true))
            {
                var target = replacer.targetObject;
                if (target == null)
                {
                    Debug.LogWarning(
                        $"[MA-CVR] ReplaceObject on '{replacer.gameObject.name}': no target set — skipped.");
                    Object.DestroyImmediate(replacer);
                    continue;
                }

                if (target.transform.IsChildOf(replacer.transform))
                {
                    Debug.LogWarning(
                        $"[MA-CVR] ReplaceObject on '{replacer.gameObject.name}': target is a child " +
                        $"of the replacement — skipped to avoid destroying the replacement itself.");
                    Object.DestroyImmediate(replacer);
                    continue;
                }

                var self = replacer.transform;
                var targetT = target.transform;

                int siblingIndex = targetT.GetSiblingIndex();
                var parent = targetT.parent;
                var name = target.name;

                Object.DestroyImmediate(target);

                self.SetParent(parent, true);
                self.SetSiblingIndex(siblingIndex);
                self.gameObject.name = name;

                Object.DestroyImmediate(replacer);
            }
        }
    }
}
#endif
