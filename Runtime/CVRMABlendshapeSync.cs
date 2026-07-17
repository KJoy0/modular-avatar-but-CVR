using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR
{
    [Serializable]
    public class CVRMABlendshapeSyncEntry
    {
        [Tooltip("This outfit's mesh — the FOLLOWER that gets driven.")]
        public SkinnedMeshRenderer localMesh;
        [BlendshapeName(nameof(localMesh))]
        [Tooltip("Blendshape on this outfit mesh to drive.")]
        public string localBlendshape;
        [Tooltip("The mesh to follow (usually the base avatar's body) — the SOURCE of truth.")]
        public SkinnedMeshRenderer targetMesh;
        [BlendshapeName(nameof(targetMesh))]
        [Tooltip("Blendshape on the followed mesh to read from.")]
        public string targetBlendshape;

        [Tooltip("Remap the source weight through a curve instead of copying it 1:1.")]
        public bool useCurve;
        [Tooltip("Maps source weight (X, 0–100) to follower weight (Y, 0–100).")]
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 100f, 100f);

        /// <summary>Follower weight for a given source weight, honoring the optional remap curve.</summary>
        public float MapWeight(float sourceWeight) =>
            useCurve && curve != null ? curve.Evaluate(sourceWeight) : sourceWeight;
    }

    /// <summary>
    /// Makes blendshapes on this mesh follow blendshapes on another mesh
    /// (usually the base avatar's body).
    ///
    /// In the editor, weights are mirrored live for preview. At build time,
    /// every animation clip that animates the followed blendshape is retargeted
    /// to also animate this mesh's blendshape, so the sync works in-game.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Blendshape Sync")]
    public class CVRMABlendshapeSync : CVRMAComponent
    {
        public List<CVRMABlendshapeSyncEntry> blendshapes = new List<CVRMABlendshapeSyncEntry>();

#if UNITY_EDITOR
        // Live editor preview: mirror source weights onto followers every frame in edit mode.
        private void Update()
        {
            if (Application.isPlaying) return;
            if (blendshapes == null) return;

            foreach (var entry in blendshapes)
            {
                if (entry?.localMesh == null || entry.targetMesh == null) continue;
                if (entry.localMesh.sharedMesh == null || entry.targetMesh.sharedMesh == null) continue;
                if (string.IsNullOrEmpty(entry.localBlendshape) ||
                    string.IsNullOrEmpty(entry.targetBlendshape)) continue;

                int srcIdx = entry.targetMesh.sharedMesh.GetBlendShapeIndex(entry.targetBlendshape);
                int dstIdx = entry.localMesh.sharedMesh.GetBlendShapeIndex(entry.localBlendshape);
                if (srcIdx < 0 || dstIdx < 0) continue;

                float src = entry.MapWeight(entry.targetMesh.GetBlendShapeWeight(srcIdx));
                if (!Mathf.Approximately(entry.localMesh.GetBlendShapeWeight(dstIdx), src))
                    entry.localMesh.SetBlendShapeWeight(dstIdx, src);
            }
        }
#endif
    }
}
