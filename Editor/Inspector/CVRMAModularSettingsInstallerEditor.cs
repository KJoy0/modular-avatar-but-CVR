#if UNITY_EDITOR
using ABI.CCK.Scripts;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMAModularSettingsInstaller))]
    internal class CVRMAModularSettingsInstallerEditor : UnityEditor.Editor
    {
        private const int MaxSyncBits = 3200;

        private SerializedProperty _modularSettings;
        private SerializedProperty _laterOverridesEarlier;

        private void OnEnable()
        {
            _modularSettings        = serializedObject.FindProperty("modularSettings");
            _laterOverridesEarlier  = serializedObject.FindProperty("laterOverridesEarlier");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Modular Settings Installer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Installs one or more reusable Modular Settings assets onto the avatar.\n\n" +
                "At build time the entries from each asset are appended to the avatar's " +
                "Advanced Avatar Settings. Click 'Sync to Avatar Now' to preview the result " +
                "in the CVR avatar inspector without having to build.",
                MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_modularSettings,        new GUIContent("Modular Settings"), true);
            EditorGUILayout.PropertyField(_laterOverridesEarlier,  new GUIContent("Later Overrides Earlier"));

            var installer = (CVRMAModularSettingsInstaller)target;
            var avatarRoot = FindAvatarRoot(installer.transform);

            // Sync-bit usage estimate
            EditorGUILayout.Space(8);
            DrawSyncUsage(installer, avatarRoot);

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(avatarRoot == null))
            {
                if (GUILayout.Button("Sync to Avatar Now", GUILayout.Height(30)))
                    SyncNow(installer, avatarRoot);
            }

            if (avatarRoot == null)
                EditorGUILayout.HelpBox("No CVRAvatar found in parents.", MessageType.Warning);

            serializedObject.ApplyModifiedProperties();
        }

        private static void SyncNow(CVRMAModularSettingsInstaller installer, GameObject avatarRoot)
        {
            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            if (avatar == null) return;

            Undo.RecordObject(avatar, "Sync Modular Settings to Avatar");
            int added = CVRMAModularSettingsInstallerPass.Apply(avatar, installer);
            EditorUtility.SetDirty(avatar);
            Debug.Log($"[MA-CVR] Modular Settings: synced — {added} new entries added.");
        }

        private static void DrawSyncUsage(CVRMAModularSettingsInstaller installer, GameObject avatarRoot)
        {
            int bits = EstimateSyncBits(installer);
            int avatarBits = avatarRoot != null ? EstimateAvatarSyncBits(avatarRoot) : 0;
            int totalIfSynced = avatarBits + bits;

            var rect = GUILayoutUtility.GetRect(18f, 18f, "TextField");
            EditorGUI.ProgressBar(
                rect,
                Mathf.Clamp01(totalIfSynced / (float)MaxSyncBits),
                $"({totalIfSynced} / {MaxSyncBits}) Synced Bits — installer adds {bits}");
        }

        private static int EstimateSyncBits(CVRMAModularSettingsInstaller installer)
        {
            int bits = 0;
            if (installer.modularSettings == null) return 0;
            foreach (var bundle in installer.modularSettings)
            {
                if (bundle?.settings == null) continue;
                foreach (var entry in bundle.settings)
                    bits += BitsForEntry(entry);
            }
            return bits;
        }

        private static int EstimateAvatarSyncBits(GameObject avatarRoot)
        {
            var avatar = avatarRoot.GetComponent<ABI.CCK.Components.CVRAvatar>();
            if (avatar?.avatarSettings?.settings == null) return 0;
            int bits = 0;
            foreach (var entry in avatar.avatarSettings.settings)
                bits += BitsForEntry(entry);
            return bits;
        }

        /// <summary>
        /// Rough sync-bit estimate per AAS entry. Matches CVR's general accounting:
        /// floats = 32, ints = 32, bools = 8 (not packed here for simplicity),
        /// colors / joysticks count their channels.
        /// </summary>
        private static int BitsForEntry(CVRAdvancedSettingsEntry entry)
        {
            if (entry == null) return 0;
            // Skip local-only (machine name starting with '#')
            if (!string.IsNullOrEmpty(entry.machineName) && entry.machineName.StartsWith("#"))
                return 0;

            switch (entry.type)
            {
                case CVRAdvancedSettingsEntry.SettingsType.Toggle:        return 8;
                case CVRAdvancedSettingsEntry.SettingsType.Dropdown:      return 32;
                case CVRAdvancedSettingsEntry.SettingsType.Slider:        return 32;
                case CVRAdvancedSettingsEntry.SettingsType.InputSingle:   return 32;
                case CVRAdvancedSettingsEntry.SettingsType.InputVector2:  return 64;
                case CVRAdvancedSettingsEntry.SettingsType.InputVector3:  return 96;
                case CVRAdvancedSettingsEntry.SettingsType.Joystick2D:    return 64;
                case CVRAdvancedSettingsEntry.SettingsType.Joystick3D:    return 96;
                case CVRAdvancedSettingsEntry.SettingsType.Color:         return 96; // 3 floats
                default: return 32;
            }
        }

        private static GameObject FindAvatarRoot(Transform t)
        {
            while (t != null)
            {
                if (t.GetComponent<ABI.CCK.Components.CVRAvatar>() != null) return t.gameObject;
                t = t.parent;
            }
            return null;
        }
    }
}
#endif
