#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Shared chrome for every MA-CVR inspector: the branded header, the
    /// "this needs to be inside your avatar" check, and section dividers.
    /// </summary>
    internal static class CVRMAInspectorUI
    {
        /// <summary>
        /// The logo's orange, sampled from the art — RGB(255, 85, 15). Orange distinguishes
        /// the CVR build from VRC Modular Avatar's blue.
        /// </summary>
        internal static readonly Color Accent = new Color(1f, 85f / 255f, 15f / 255f);

        private const string LogoAssetName = "MA_CVR_Logo";
        private const float LogoAspect = 2057f / 572f;
        private const float MaxLogoWidth = 280f;
        private const float LogoPadding = 4f;

        private static Texture2D _logo;
        private static bool _logoSearched;

        private static Texture2D Logo
        {
            get
            {
                // Found by name rather than a hard-coded path, so moving or renaming the
                // package folder doesn't break the header.
                if (_logo == null && !_logoSearched)
                {
                    _logoSearched = true;
                    foreach (var guid in AssetDatabase.FindAssets($"{LogoAssetName} t:Texture2D"))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (System.IO.Path.GetFileNameWithoutExtension(path) != LogoAssetName) continue;
                        _logo = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (_logo != null) break;
                    }
                }
                return _logo;
            }
        }

        /// <summary>
        /// Standard header: logo, component title, and a warning when the component isn't
        /// inside an avatar (where it would silently do nothing at build).
        /// </summary>
        internal static void DrawHeader(Component target, string title)
        {
            DrawLogo();
            DrawTitle(title);
            DrawAvatarWarning(target);
        }

        private static void DrawLogo()
        {
            var logo = Logo;
            if (logo == null) return;

            float width = Mathf.Min(EditorGUIUtility.currentViewWidth - 40f, MaxLogoWidth);
            if (width < 80f) return; // inspector too narrow for the logo to read

            float height = width / LogoAspect;
            var row = GUILayoutUtility.GetRect(0f, height + LogoPadding * 2f, GUILayout.ExpandWidth(true));

            // The wordmark baked into the art is white, so it disappears against Unity's light
            // skin. Give it a dark plate to sit on there; the dark skin needs nothing.
            if (!EditorGUIUtility.isProSkin)
                EditorGUI.DrawRect(row, new Color(0.16f, 0.16f, 0.17f));

            var rect = new Rect(
                row.x + (row.width - width) * 0.5f, row.y + LogoPadding, width, height);
            GUI.DrawTexture(rect, logo, ScaleMode.ScaleToFit, true);
        }

        private static void DrawTitle(string title)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };
            EditorGUILayout.LabelField(title, style);
            EditorGUILayout.Space(2);
        }

        /// <summary>
        /// Every MA-CVR component is processed by walking down from the CVRAvatar, so one sitting
        /// outside an avatar is silently inert at build. Say so, the way VRC MA does.
        /// Returns true when the component is correctly placed.
        /// </summary>
        internal static bool DrawAvatarWarning(Component target)
        {
            if (target == null) return true;
            if (target.GetComponentInParent<ABI.CCK.Components.CVRAvatar>(true) != null) return true;

            EditorGUILayout.HelpBox(
                "This component needs to be placed inside your avatar to work properly.",
                MessageType.Warning);
            return false;
        }

        /// <summary>A labelled divider, for grouping related fields within one inspector.</summary>
        internal static void Section(string title)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
    }
}
#endif
