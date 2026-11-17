using UnityEngine;

namespace PacificRimUI
{
    public enum PacificRimTextStyle
    {
        Display,
        Title,
        Heading,
        Body,
        Small,
        Caption,
        Number
    }

    public struct TextStyleSpec
    {
        public int Size;
        public int Tracking;
        public FontStyle FontStyle;
        public bool Upper;
    }

    /// <summary>
    /// Direct C# translation of the Battlefront UI.Theme kit,
    /// replacing the crimson red styling with a high-tech Pacific Rim blue-ish aesthetic.
    /// Roblox Transparency values (0 = opaque, 1 = invisible) are converted to Unity alpha (1 - Transparency).
    /// </summary>
    public static class PacificRimUITheme
    {
        // Core Colors (translated from UI.Theme)
        public static readonly Color Background       = new Color(6f / 255f, 6f / 255f, 9f / 255f, 1.0f);       // RGB(6, 6, 9)
        public static readonly Color Panel            = new Color(8f / 255f, 12f / 255f, 18f / 255f, 0.68f);    // Blue-tinted translucent glass panel
        public static readonly Color PanelOpaque      = new Color(8f / 255f, 11f / 255f, 17f / 255f, 0.97f);    // Modal / dialog fill (prevents underlying text bleed)
        public static readonly Color Line             = new Color(1.0f, 1.0f, 1.0f, 0.22f);                     // RGB(255,255,255) @ LineTransparency 0.78
        public const float LineTransparency           = 0.78f;
        public const float LineAlpha                  = 0.22f;

        // Typography Colors
        public static readonly Color Text             = new Color(242f / 255f, 242f / 255f, 244f / 255f, 1.0f); // RGB(242, 242, 244)
        public static readonly Color TextDim          = new Color(175f / 255f, 172f / 255f, 178f / 255f, 1.0f); // RGB(175, 172, 178)
        public static readonly Color TextMuted        = new Color(115f / 255f, 110f / 255f, 118f / 255f, 1.0f); // RGB(115, 110, 118)

        // Blue Accent Palette (replaces Red / RedBright / RedDeep)
        public static readonly Color Blue             = new Color(36f / 255f, 148f / 255f, 226f / 255f, 1.0f);  // Replaces Red RGB(226, 36, 48)
        public static readonly Color BlueBright       = new Color(70f / 255f, 195f / 255f, 255f / 255f, 1.0f);  // Replaces RedBright RGB(255, 70, 80)
        public static readonly Color BlueDeep         = new Color(8f / 255f, 38f / 255f, 110f / 255f, 1.0f);    // Replaces RedDeep RGB(110, 8, 16)
        public static readonly Color CardFill         = new Color(6f / 255f, 28f / 255f, 70f / 255f, 0.45f);    // Replaces RGB(70, 6, 12) @ 0.55 transparency
        public static readonly Color RowFill          = new Color(12f / 255f, 16f / 255f, 24f / 255f, 0.55f);   // Unselected row @ 0.45 transparency
        public static readonly Color RowSelectedFill  = new Color(6f / 255f, 28f / 255f, 70f / 255f, 0.75f);    // Selected row @ 0.25 transparency

        // Status & Highlights
        public static readonly Color Accent           = Blue;
        public static readonly Color Amber            = new Color(255f / 255f, 150f / 255f, 60f / 255f, 1.0f);  // RGB(255, 150, 60)
        public static readonly Color Danger           = new Color(255f / 255f, 80f / 255f, 80f / 255f, 1.0f);   // RGB(255, 80, 80)
        public static readonly Color Success          = new Color(110f / 255f, 220f / 255f, 150f / 255f, 1.0f); // RGB(110, 220, 150)
        public static readonly Color Health           = new Color(242f / 255f, 242f / 255f, 244f / 255f, 1.0f); // RGB(242, 242, 244)
        public static readonly Color BarTrack         = new Color(1.0f, 1.0f, 1.0f, 0.15f);                     // White @ TrackTransparency 0.85

        // Timing & Animation
        public const float TweenDurationFast          = 0.16f;
        public const float TweenDurationNormal        = 0.20f;
        public const float TweenDurationSlow          = 0.45f;

        private static Font cachedFont;

        public static Font GetDefaultFont()
        {
            if (cachedFont == null)
            {
#if UNITY_EDITOR
                cachedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
#endif
                if (cachedFont == null)
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (cachedFont == null)
                    cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return cachedFont;
        }

        public static TextStyleSpec GetStyle(PacificRimTextStyle style)
        {
            switch (style)
            {
                case PacificRimTextStyle.Display:
                    return new TextStyleSpec { Size = 50, Tracking = 2, FontStyle = FontStyle.Normal, Upper = true };
                case PacificRimTextStyle.Title:
                    return new TextStyleSpec { Size = 30, Tracking = 2, FontStyle = FontStyle.Normal, Upper = true };
                case PacificRimTextStyle.Heading:
                    return new TextStyleSpec { Size = 22, Tracking = 1, FontStyle = FontStyle.Bold, Upper = true };
                case PacificRimTextStyle.Body:
                    return new TextStyleSpec { Size = 18, Tracking = 0, FontStyle = FontStyle.Normal, Upper = false };
                case PacificRimTextStyle.Small:
                    return new TextStyleSpec { Size = 15, Tracking = 0, FontStyle = FontStyle.Normal, Upper = false };
                case PacificRimTextStyle.Caption:
                    return new TextStyleSpec { Size = 14, Tracking = 2, FontStyle = FontStyle.Bold, Upper = true };
                case PacificRimTextStyle.Number:
                    return new TextStyleSpec { Size = 30, Tracking = 0, FontStyle = FontStyle.Bold, Upper = false };
                default:
                    return new TextStyleSpec { Size = 18, Tracking = 0, FontStyle = FontStyle.Normal, Upper = false };
            }
        }

        /// <summary>
        /// Formats UI labels with consistent casing without inserting artificial spaces between characters.
        /// </summary>
        public static string Track(string text, int tracking, int baseFontSize = 18, bool upper = true)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return upper ? text.ToUpperInvariant() : text;
        }

        /// <summary>
        /// Equivalent of UI.formatNumber(n): formats an integer with comma thousands separators.
        /// </summary>
        public static string FormatNumber(int n)
        {
            return n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
