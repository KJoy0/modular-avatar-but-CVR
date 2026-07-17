#if UNITY_EDITOR
using ABI.CCK.Scripts;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Shared helpers for writing AAS Toggle entries from reactive passes
    /// (ShapeChanger / MaterialSwap / MaterialSetter / ObjectToggle).
    ///
    /// Several reactive components may share one parameter — e.g. a Menu Item whose
    /// toggle both shows an object and drives a blendshape. In that case there must be
    /// exactly ONE AAS entry per machine name; additional components MERGE their
    /// animation curves into the existing entry's ON/OFF clips instead of adding a
    /// duplicate entry (duplicate machine names would make CVR generate two fighting
    /// animator layers).
    /// </summary>
    internal static class CVRMAAASUtil
    {
        /// <summary>
        /// Adds the entry, or merges its clips into an existing entry with the same
        /// machine name. Returns true if anything was applied.
        /// </summary>
        internal static bool AddOrMergeToggleEntry(
            ABI.CCK.Components.CVRAvatar avatar, CVRAdvancedSettingsEntry entry)
        {
            if (entry == null) return false;

            var settings = avatar.avatarSettings.settings;
            var existing = settings.Find(s => s.machineName == entry.machineName);
            if (existing == null)
            {
                settings.Add(entry);
                return true;
            }

            // Merge path: both must be clip-based toggles.
            var src = entry.toggleSettings;
            var dst = existing.toggleSettings;

            if (existing.type != CVRAdvancedSettingsEntry.SettingsType.Toggle || dst == null || src == null)
            {
                Debug.LogWarning(
                    $"[MA-CVR] AAS entry '{entry.machineName}' already exists with an incompatible " +
                    $"type — the additional reactive component's animation was NOT merged.");
                return false;
            }

            if (!src.useAnimationClip)
                return false; // nothing to merge (entry without clips, e.g. ObjectToggle slim entry)

            if (!dst.useAnimationClip)
            {
                // Existing entry is a plain toggle — attach our clips to it.
                dst.useAnimationClip = true;
                dst.animationClip    = src.animationClip;
                dst.offAnimationClip = src.offAnimationClip;
                return true;
            }

            // Both have clips: append our curves into the existing temp clips.
            MergeCurves(src.animationClip,    dst.animationClip);
            MergeCurves(src.offAnimationClip, dst.offAnimationClip);
            return true;
        }

        /// <summary>Copies every float and object-reference curve from src into dst.</summary>
        private static void MergeCurves(AnimationClip src, AnimationClip dst)
        {
            if (src == null || dst == null) return;

            foreach (var binding in AnimationUtility.GetCurveBindings(src))
            {
                AnimationUtility.SetEditorCurve(dst, binding,
                    AnimationUtility.GetEditorCurve(src, binding));
            }

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(src))
            {
                AnimationUtility.SetObjectReferenceCurve(dst, binding,
                    AnimationUtility.GetObjectReferenceCurve(src, binding));
            }

            EditorUtility.SetDirty(dst);
        }
    }
}
#endif
