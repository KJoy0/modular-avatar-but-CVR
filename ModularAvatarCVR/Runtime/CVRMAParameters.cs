using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR
{
    public enum CVRMAParameterSyncType
    {
        /// <summary>Not network-synced; exists only in the local animator.</summary>
        NotSynced,
        Int,
        Float,
        Bool
    }

    [Serializable]
    public class CVRMAParameterConfig
    {
        [Tooltip("Animator parameter name (or prefix when isPrefix is true).")]
        public string nameOrPrefix;

        [Tooltip("Remap this parameter to a different name in the avatar's AAS.")]
        public string remapTo;

        public CVRMAParameterSyncType syncType = CVRMAParameterSyncType.NotSynced;

        public float defaultValue;
        public bool saved = true;
        public bool localOnly;

        [Tooltip("If true, nameOrPrefix is a prefix matching multiple parameters.")]
        public bool isPrefix;
    }

    /// <summary>
    /// Declares animator parameters so they are registered in the avatar's Advanced
    /// Avatar Settings at build time. Useful for parameters driven purely by animators
    /// that need network sync without a visible menu entry.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Parameters")]
    [DisallowMultipleComponent]
    public class CVRMAParameters : CVRMAComponent
    {
        public List<CVRMAParameterConfig> parameters = new List<CVRMAParameterConfig>();
    }
}
