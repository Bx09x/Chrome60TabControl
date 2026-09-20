using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Chrome60Tabs.Rendering;

namespace Chrome60Tabs
{
    /// <summary>
    /// Paints the Chrome 60 tab strip. All methods are virtual so the look can be tweaked by
    /// subclassing and assigning <see cref="Chrome60TabControl.Renderer"/>.
    /// <para>
    /// Paint order mirrors Chromium M60 <c>TabStrip::PaintChildren</c> in Material Design mode:
    /// new tab button behind (parallelogram peeking from behind the last tab), then inactive
    /// tabs back-to-front with fill+stroke together per tab (so each overlap shows one clean
    /// edge, the covered slope ghosting faintly through the ~90% translucent fill),
    /// the toolbar top line, then the active tab on top.
    /// </para>
    /// </summary>
    public class Chrome60TabRenderer
    {
        private const TextFormatFlags TitleFlags =
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping;

        public virtual void DrawBackground(Graphics g, Chrome60TabControl control, Rectangle bounds)
        {
            using (var brush = new SolidBrush(control.TabStripColor))
                g.FillRectangle(brush, bounds);
        }

        /// <summary>
        /// Fill of a background tab, including its hover glow.
        /// <para>
        /// Chrome 60 background tabs are very slightly translucent. Tabs are painted
        /// back-to-front with fill+stroke together, so the tab on top covers the
        /// neighbour's slope in the 16px overlap and each gap shows one clean edge.
        /// At ~90% opacity the covered edge still ghosts through very faintly instead
        /// of producing a harsh doubled "X" when many tabs shrink to minimum width.
        /// AntiAlias stays on so edges blend smoothly.
        /// </para>
        /// </summary>
        public virtual void DrawInactiveTabFill(Graphics g, Chrome60TabControl control, Chrome60TabLayout layout)
        {
            var opaque = Chrome60Colors.Blend(control.InactiveTabColor, control.InactiveTabHoverColor, layout.Tab.HoverProgress);
            // Slight transparency: 230/255 ~= 90% opaque. Enough to soften stacked overlaps,
            // subtle enough to still look solid like the reference screenshot.
            var fill = Color.FromArgb(230, opaque);
            var state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, layout.FillPath);
            }
            finally
            {
                g.Restore(state);
            }
        }

        public virtual void DrawActiveTabFill(Graphics g, Chrome60TabControl control, Chrome60TabLayout layout)
        {
            using (var brush = new SolidBrush(control.ActiveTabColor))
                g.FillPath(brush, layout.FillPath);
        }

        public virtual void DrawTabStroke(Graphics g, Chrome60TabControl control, Chrome60TabLayout layout)
        {
            using (var pen = new Pen(control.TabBorderColor, 1f))
                g.DrawPath(pen, layout.StrokePath);
        }

        /// <summary>The 1-pixel line separating the tab strip from the toolbar. Drawn before the active tab so the tab covers it.</summary>
        public virtual void DrawToolbarLine(Graphics g, Chrome60TabControl control, Rectangle stripBounds)
        {
            float y = stripBounds.Bottom - 0.5f;
            using (var pen = new Pen(control.TabBorderColor, 1f))
                g.DrawLine(pen, stripBounds.Left, y, stripBounds.Right, y);
        }

        /// <summary>Favicon, title and close box.</summary>
        public virtual void DrawTabContent(Graphics g, Chrome60TabControl control, Chrome60TabLayout layout, float scale)
        {
            var tab = layout.Tab;

            var fill = layout.IsActive
                ? control.ActiveTabColor
                : Chrome60Colors.Blend(control.InactiveTabColor, control.InactiveTabHoverColor, tab.HoverProgress);

            if (layout.ShowIcon)
                DrawIcon(g, control, layout, scale);

            if (layout.TextBounds.Width > 0 && !string.IsNullOrEmpty(tab.Text))
            {
                Color textColor = !tab.Enabled || !control.Enabled
                    ? control.DisabledTabTextColor
                    : (layout.IsActive ? control.TabTextColor : control.InactiveTabTextColor);
                DrawTitle(g, control.Font, tab.Text, layout.TextBounds, textColor, fill, scale);
            }

            if (layout.ShowCloseButton)
                DrawCloseButton(g, control, layout, control.GetCloseButtonState(tab), scale);
        }

        public virtual void DrawIcon(Graphics g, Chrome60TabControl control, Chrome60TabLayout layout, float scale)
        {
            var tab = layout.Tab;
            float opacity = tab.Enabled && control.Enabled ? 1f : 0.5f;

            if (tab.Icon != null)
            {
                Chrome60Icons.DrawImage(g, tab.Icon, layout.IconBounds, opacity);
            }
            else if (control.ShowDefaultIcon)
            {
                var color = Color.FromArgb((int)(control.DefaultIconColor.A * opacity), control.DefaultIconColor);
                Chrome60Icons.DrawDefaultFavicon(g, layout.IconBounds, color, scale);
            }
        }

        /// <summary>
        /// Draws the title left-aligned and vertically centred. When it does not fit, the tail is
        /// faded into the tab fill instead of being cut with an ellipsis (Chrome's FADE_TAIL).
        /// </summary>
        public virtual void DrawTitle(Graphics g, Font font, string text, Rectangle bounds, Color color, Color fill, float scale)
        {
            var measured = TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, bounds.Height), TitleFlags);

            if (measured.Width <= bounds.Width)
            {
                TextRenderer.DrawText(g, text, font, bounds, color, TitleFlags);
                return;
            }

            var state = g.Save();
            try
            {
                g.SetClip(bounds, CombineMode.Intersect);
                var wide = new Rectangle(bounds.X, bounds.Y, measured.Width + 2, bounds.Height);
                TextRenderer.DrawText(g, text, font, wide, color, TitleFlags);
            }
            finally
            {
                g.Restore(state);
            }

            int fadeWidth = Math.Min(bounds.Width, (int)Math.Round(Chrome60Metrics.TitleFadeWidth * scale));
            if (fadeWidth < 2) return;

            var fadeRect = new Rectangle(bounds.Right - fadeWidth, bounds.Y, fadeWidth, bounds.Height);
            // The gradient rectangle is widened by a pixel on each side so GDI+ never wraps the gradient at the edges.
            var gradientRect = new Rectangle(fadeRect.X - 1, fadeRect.Y, fadeRect.Width + 2, fadeRect.Height);
            using (var brush = new LinearGradientBrush(gradientRect, Color.FromArgb(0, fill), fill, LinearGradientMode.Horizontal))
                g.FillRectangle(brush, fadeRect);
        }

        public virtual void DrawCloseButton(Graphics g, Chrome60TabControl control, Chrome60TabLayout layout, Chrome60ButtonState state, float scale)
        {
            var box = layout.CloseButtonBounds;

            switch (state)
            {
                case Chrome60ButtonState.Hot:
                    Chrome60Icons.DrawCloseCircle(g, box, control.CloseButtonHoverColor);
                    Chrome60Icons.DrawCloseGlyph(g, box, control.CloseButtonHoverGlyphColor, scale);
                    break;
                case Chrome60ButtonState.Pressed:
                    Chrome60Icons.DrawCloseCircle(g, box, control.CloseButtonPressedColor);
                    Chrome60Icons.DrawCloseGlyph(g, box, control.CloseButtonHoverGlyphColor, scale);
                    break;
                default:
                    var color = layout.IsActive ? control.CloseButtonColor : control.InactiveCloseButtonColor;
                    if (!layout.Tab.Enabled) color = Color.FromArgb(color.A / 2, color);
                    Chrome60Icons.DrawCloseGlyph(g, box, color, scale);
                    break;
            }
        }

        public virtual void DrawNewTabButton(Graphics g, Chrome60TabControl control, Rectangle bounds, GraphicsPath fillPath, Chrome60ButtonState state, float hoverProgress, float scale)
        {
            Color opaque;
            switch (state)
            {
                case Chrome60ButtonState.Pressed:
                    opaque = control.NewTabButtonPressedColor;
                    break;
                default:
                    opaque = Chrome60Colors.Blend(control.NewTabButtonColor, control.NewTabButtonHoverColor, hoverProgress);
                    break;
            }

            // Same slight translucency as background tabs so the 5px tuck-under region
            // blends with the last tab instead of hard-covering its slope.
            var fill = state == Chrome60ButtonState.Pressed
                ? opaque
                : Color.FromArgb(210, opaque);

            var mode = g.SmoothingMode;
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, fillPath);

                using (var strokePath = Chrome60Geometry.CreateNewTabButtonPath(Chrome60Geometry.StrokeBoundsForClosedShape(bounds), scale))
                using (var pen = new Pen(control.TabBorderColor, 1f))
                    g.DrawPath(pen, strokePath);
            }
            finally
            {
                g.SmoothingMode = mode;
            }

            if (control.ShowNewTabButtonPlus)
                Chrome60Icons.DrawPlusGlyph(g, bounds, control.NewTabButtonGlyphColor, scale);
        }

        /// <summary>Dotted focus rectangle around the selected tab's title when keyboard focus cues are on.</summary>
        public virtual void DrawFocusCue(Graphics g, Chrome60TabControl control, Chrome60TabLayout layout)
        {
            var r = layout.TextBounds;
            if (r.Width <= 2 || r.Height <= 2) return;
            r.Inflate(1, -2);
            ControlPaint.DrawFocusRectangle(g, r, control.TabTextColor, control.ActiveTabColor);
        }
    }
}
