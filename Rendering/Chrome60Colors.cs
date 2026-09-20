using System.Drawing;

namespace Chrome60Tabs.Rendering
{
    /// <summary>
    /// Default light-theme colours of Chrome 60 (Material Design desktop theme, 2016-2018).
    /// Sources: Chromium M60 <c>theme_properties.cc</c> (kDefaultColorFrame, kDefaultColorToolbar,
    /// kDefaultColorBackgroundTabText) and <c>tab.cc</c> / <c>new_tab_button.cc</c> stroke and
    /// close-button constants.
    /// </summary>
    public static class Chrome60Colors
    {
        /// <summary>kDefaultColorFrame - the frame / tab strip background of an active window.</summary>
        public static readonly Color Frame = Color.FromArgb(0xDE, 0xE1, 0xE6);

        /// <summary>kDefaultColorFrameInactive - frame colour when the window is not focused.</summary>
        public static readonly Color FrameInactive = Color.FromArgb(0xE7, 0xEA, 0xED);

        /// <summary>kDefaultColorToolbar - the toolbar and therefore the active tab.</summary>
        public static readonly Color Toolbar = Color.FromArgb(0xF2, 0xF2, 0xF2);

        /// <summary>Inactive (background) tab fill: frame blended toward the toolbar colour.</summary>
        public static readonly Color InactiveTab = Color.FromArgb(0xE8, 0xEA, 0xED);

        /// <summary>Inactive tab fill at full hover glow.</summary>
        public static readonly Color InactiveTabHover = Color.FromArgb(0xEE, 0xF0, 0xF2);

        /// <summary>Tab / toolbar stroke: black at ~25% over the frame.</summary>
        public static readonly Color Stroke = Color.FromArgb(0xA6, 0xA9, 0xAD);

        /// <summary>kDefaultColorTabText.</summary>
        public static readonly Color TabText = Color.FromArgb(0x00, 0x00, 0x00);

        /// <summary>kDefaultColorBackgroundTabText.</summary>
        public static readonly Color InactiveTabText = Color.FromArgb(0x64, 0x64, 0x64);

        /// <summary>Text colour of a disabled tab.</summary>
        public static readonly Color DisabledTabText = Color.FromArgb(0xA0, 0xA0, 0xA0);

        /// <summary>Close X on the active tab.</summary>
        public static readonly Color CloseButton = Color.FromArgb(0x5A, 0x5A, 0x5A);

        /// <summary>Close X on inactive tabs.</summary>
        public static readonly Color InactiveCloseButton = Color.FromArgb(0x7B, 0x7B, 0x7B);

        /// <summary>Hovered close button circle (Google red).</summary>
        public static readonly Color CloseButtonHover = Color.FromArgb(0xDB, 0x44, 0x37);

        /// <summary>Pressed close button circle.</summary>
        public static readonly Color CloseButtonPressed = Color.FromArgb(0xA8, 0x35, 0x2A);

        /// <summary>Close X glyph while hovered or pressed.</summary>
        public static readonly Color CloseButtonGlyphOnHover = Color.White;

        /// <summary>New tab button fill (same as an inactive tab).</summary>
        public static readonly Color NewTabButton = InactiveTab;

        /// <summary>New tab button fill while hovered.</summary>
        public static readonly Color NewTabButtonHover = Color.FromArgb(0xF5, 0xF6, 0xF7);

        /// <summary>New tab button fill while pressed.</summary>
        public static readonly Color NewTabButtonPressed = Color.FromArgb(0xD4, 0xD7, 0xDB);

        /// <summary>Optional plus glyph on the new tab button.</summary>
        public static readonly Color NewTabButtonGlyph = Color.FromArgb(0x5A, 0x5A, 0x5A);

        /// <summary>Default favicon glyph colour (grey page icon).</summary>
        public static readonly Color DefaultFavicon = Color.FromArgb(0x9E, 0x9E, 0x9E);

        /// <summary>Linear interpolation between two colours. <paramref name="t"/> is clamped to 0..1.</summary>
        public static Color Blend(Color from, Color to, float t)
        {
            if (t <= 0f) return from;
            if (t >= 1f) return to;
            return Color.FromArgb(
                Lerp(from.A, to.A, t),
                Lerp(from.R, to.R, t),
                Lerp(from.G, to.G, t),
                Lerp(from.B, to.B, t));
        }

        private static int Lerp(int a, int b, float t)
        {
            int v = (int)(a + (b - a) * t + 0.5f);
            if (v < 0) return 0;
            return v > 255 ? 255 : v;
        }
    }
}
