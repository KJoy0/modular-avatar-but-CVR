#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Shared armature helpers used by Setup Outfit and the Merge Armature build pass:
    /// prefix/suffix inference (Hips-anchored, like VRC MA) and Hips lookup.
    /// </summary>
    internal static class CVRMAArmatureUtil
    {
        /// <summary>
        /// Detects a common prefix/suffix wrapped around outfit bone names relative to the
        /// avatar's bone names. Anchors on the Hips bone first (unambiguous), then falls
        /// back to a longest-match scan of all bones.
        /// </summary>
        internal static void DetectPrefixSuffix(Transform outfitArmature, Transform avatarArmature,
            out string prefix, out string suffix)
        {
            prefix = "";
            suffix = "";

            var avatarHips = FindHips(avatarArmature);
            var outfitHips = FindHips(outfitArmature);
            if (avatarHips != null && outfitHips != null &&
                TryExtractAffix(outfitHips.name, avatarHips.name, out prefix, out suffix))
                return;

            var avatarBones = new List<string>();
            var avatarBoneSet = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var t in avatarArmature.GetComponentsInChildren<Transform>(true))
            {
                if (avatarBoneSet.Add(t.name))
                    avatarBones.Add(t.name);
            }
            if (avatarBones.Count == 0) return;
            avatarBones.Sort((a, b) => b.Length.CompareTo(a.Length)); // longest first

            foreach (var bone in outfitArmature.GetComponentsInChildren<Transform>(true))
            {
                if (bone == outfitArmature) continue;
                var name = bone.name;
                if (avatarBoneSet.Contains(name)) continue; // exact match — no affix here

                foreach (var avatarBone in avatarBones)
                {
                    if (TryExtractAffix(name, avatarBone, out prefix, out suffix))
                        return;
                }
            }
        }

        /// <summary>
        /// If <paramref name="outfitName"/> is <paramref name="avatarName"/> wrapped in a
        /// prefix and/or suffix, extracts them and returns true.
        /// </summary>
        internal static bool TryExtractAffix(string outfitName, string avatarName,
            out string prefix, out string suffix)
        {
            prefix = "";
            suffix = "";
            if (outfitName.Equals(avatarName, System.StringComparison.OrdinalIgnoreCase))
                return false;

            int idx = outfitName.IndexOf(avatarName, System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            prefix = outfitName.Substring(0, idx);
            suffix = outfitName.Substring(idx + avatarName.Length);
            return !string.IsNullOrEmpty(prefix) || !string.IsNullOrEmpty(suffix);
        }

        /// <summary>Finds the Hips bone: a transform whose name contains "hips" (case-insensitive).</summary>
        internal static Transform FindHips(Transform armature)
        {
            if (armature == null) return null;
            foreach (var t in armature.GetComponentsInChildren<Transform>(true))
            {
                if (t == armature) continue;
                if (t.name.IndexOf("hips", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }
            return null;
        }
    }
}
#endif
