namespace Chrome60Tabs.Rendering
{
    /// <summary>
    /// Logical (96 DPI) layout constants for the Chrome 60 tab strip.
    /// Values follow Chromium M60 <c>chrome/browser/ui/layout_constants.cc</c> and
    /// <c>chrome/browser/ui/views/tabs/tab.cc</c> (Material Design "normal" mode, non-touch).
    /// Every value is multiplied by the current DPI scale at layout/paint time.
    /// </summary>
    public static class Chrome60Metrics
    {
        /// <summary>TAB_HEIGHT (MD normal mode).</summary>
        public const int TabHeight = 29;

        /// <summary>TABSTRIP_TAB_OVERLAP. Adjacent tabs overlap by this amount, so sloped edges cross.</summary>
        public const int TabOverlap = 16;

        /// <summary>Horizontal run of one sloped end cap. Equals the overlap so the slopes of two neighbours meet.</summary>
        public const int TabEndcapWidth = 16;

        /// <summary>Left inset of the tab content area (favicon starts here).</summary>
        public const int TabContentLeftInset = 18;

        /// <summary>Right inset of the tab content area (close button ends here).</summary>
        public const int TabContentRightInset = 16;

        /// <summary>gfx::kFaviconSize.</summary>
        public const int FaviconSize = 16;

        /// <summary>TAB_FAVICON_TITLE_SPACING.</summary>
        public const int FaviconTitleSpacing = 4;

        /// <summary>Gap between end of title and the close button.</summary>
        public const int TitleCloseSpacing = 4;

        /// <summary>Visual size of the close button (the X glyph lives inside a 16x16 box).</summary>
        public const int CloseButtonSize = 16;

        /// <summary>Extra hit-test padding around the close button (Chrome enlarges the target).</summary>
        public const int CloseButtonHitPadding = 4;

        /// <summary>Tab::GetStandardSize() width - the maximum width of a tab.</summary>
        public const int StandardTabWidth = 192;

        /// <summary>Tab::GetMinimumInactiveSize() width - only the two end caps plus a sliver.</summary>
        public const int MinimumInactiveTabWidth = 36;

        /// <summary>Tab::GetMinimumActiveSize() width - room for favicon and close box.</summary>
        public const int MinimumActiveTabWidth = 56;

        /// <summary>Inactive tabs only get a close box once they are at least this wide.</summary>
        public const int MinimumInactiveWidthForCloseButton = 72;

        /// <summary>Width (bounds) of the new tab button parallelogram, including its skew.</summary>
        public const int NewTabButtonWidth = 32;

        /// <summary>Height of the new tab button parallelogram. Much shorter than a tab (29).</summary>
        public const int NewTabButtonHeight = 16;

        /// <summary>TABSTRIP_NEW_TAB_BUTTON_OVERLAP - the button tucks under the last tab's slope.</summary>
        public const int NewTabButtonOverlap = 5;

        /// <summary>
        /// Distance from the top of the tabs to the top of the new tab button at 96 DPI.
        /// Kept for reference; layout now vertically centers the button in the tab row
        /// ((TabHeight - NewTabButtonHeight) / 2 ~= 6) so it stays centered at any DPI.
        /// </summary>
        public const int NewTabButtonTopOffset = 6;

        /// <summary>Default space above the tabs (the part of the frame that would show above the tabs).</summary>
        public const int DefaultTopPadding = 6;

        /// <summary>Width of the fade applied to titles that do not fit (Chrome uses FADE_TAIL eliding).</summary>
        public const int TitleFadeWidth = 24;

        /// <summary>Radius-like size of the tiny corner curves at each end of a slope.</summary>
        public const float CornerSize = 2f;

        /// <summary>Vertical drag distance before a tab is torn off (when detaching is enabled).</summary>
        public const int DetachThreshold = 40;
    }
}
