#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    internal static class CVRMABoneProxyPass
    {
        internal static void Run(GameObject avatarRoot)
        {
            var proxies = new List<CVRMABoneProxy>(avatarRoot.GetComponentsInChildren<CVRMABoneProxy>(true));

            foreach (var proxy in proxies)
            {
                if (proxy == null) continue;
                var target = ResolveTarget(proxy);
                if (target == null)
                {
                    Debug.LogWarning($"[MA-CVR] BoneProxy on '{proxy.gameObject.name}' has no valid target — skipped.", proxy);
                    continue;
                }

                var proxyTransform = proxy.transform;
                var worldPos = proxyTransform.position;
                var worldRot = proxyTransform.rotation;
                var worldScale = proxyTransform.lossyScale;

                proxyTransform.SetParent(target, false);

                switch (proxy.attachmentMode)
                {
                    case CVRMABoneProxyAttachment.AsChildAtRoot:
                        proxyTransform.localPosition = Vector3.zero;
                        proxyTransform.localRotation = Quaternion.identity;
                        break;
                    case CVRMABoneProxyAttachment.AsChildKeepWorldPose:
                        proxyTransform.position = worldPos;
                        proxyTransform.rotation = worldRot;
                        break;
                    case CVRMABoneProxyAttachment.AsChildKeepRotation:
                        proxyTransform.position = worldPos;
                        break;
                    case CVRMABoneProxyAttachment.AsChildKeepPosition:
                        proxyTransform.rotation = worldRot;
                        break;
                }

                if (proxy.matchScale)
                {
                    var parentScale = target.lossyScale;
                    proxyTransform.localScale = new Vector3(
                        parentScale.x == 0 ? 1 : worldScale.x / parentScale.x,
                        parentScale.y == 0 ? 1 : worldScale.y / parentScale.y,
                        parentScale.z == 0 ? 1 : worldScale.z / parentScale.z
                    );
                }

                Object.DestroyImmediate(proxy);
            }
        }

        private static Transform ResolveTarget(CVRMABoneProxy proxy)
        {
            if (proxy.target == null) return null;
            if (string.IsNullOrEmpty(proxy.subPath)) return proxy.target;
            return proxy.target.Find(proxy.subPath);
        }
    }
}
#endif
