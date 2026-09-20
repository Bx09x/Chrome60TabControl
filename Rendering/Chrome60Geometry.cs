using System.Drawing;
using System.Drawing.Drawing2D;

namespace Chrome60Tabs.Rendering
{
    /// <summary>
    /// Builds the Chrome 60 tab and new-tab-button silhouettes as <see cref="GraphicsPath"/> objects.
    /// All coordinates are device pixels; <c>scale</c> is DPI / 96.
    /// <para>
    /// The Chrome 60 (Material Design) tab is a trapezoid: each side rises from the bottom with a
    /// ~2:1 slope over a 16 px end cap, with tiny 1.5-2 px curves where the slope meets the baseline
    /// and the top edge. Adjacent tabs overlap by the full end cap width, so the slopes of two
    /// neighbours cross in an "X" that is visible between inactive tabs.
    /// </para>
    /// </summary>
    public static class Chrome60Geometry
    {
        /// <summary>
        /// Creates the outline of one tab. The bottom edge is left open (the tab merges into the
        /// toolbar). Set <paramref name="closeFigure"/> to true for fill / hit-test paths.
        /// </summary>
        public static GraphicsPath CreateTabPath(RectangleF bounds, float scale, bool closeFigure)
        {
            float e = Chrome60Metrics.TabEndcapWidth * scale;
            float c = Chrome60Metrics.CornerSize * scale;

            // Very narrow tabs: shrink the end caps so the path never folds over itself.
            float maxEndcap = (bounds.Width - 2f * c) / 2f;
            if (e > maxEndcap) e = maxEndcap < c ? c : maxEndcap;

            float l = bounds.Left;
            float r = bounds.Right;
            float t = bounds.Top;
            float b = bounds.Bottom;

            var path = new GraphicsPath();
            path.StartFigure();

            // Bottom-left foot: baseline flares up into the slope.
            AddQuad(path, new PointF(l, b), new PointF(l + c * 0.6f, b), new PointF(l + c, b - c));
            // Left slope.
            path.AddLine(l + c, b - c, l + e - c, t + c);
            // Top-left shoulder.
            AddQuad(path, new PointF(l + e - c, t + c), new PointF(l + e - c * 0.5f, t), new PointF(l + e + c, t));
            // Top edge.
            path.AddLine(l + e + c, t, r - e - c, t);
            // Top-right shoulder.
            AddQuad(path, new PointF(r - e - c, t), new PointF(r - e + c * 0.5f, t), new PointF(r - e + c, t + c));
            // Right slope.
            path.AddLine(r - e + c, t + c, r - c, b - c);
            // Bottom-right foot.
            AddQuad(path, new PointF(r - c, b - c), new PointF(r - c * 0.6f, b), new PointF(r, b));

            if (closeFigure)
                path.CloseFigure();

            return path;
        }

        /// <summary>
        /// Creates the outline of the Chrome 60 new tab button.
        /// <para>
        /// Unlike a tab (a symmetric trapezoid, wider at the bottom), the real Chrome 60
        /// new-tab button is a skewed parallelogram: both slanted edges lean the same way
        /// (down-right, parallel to a tab's right slope) with a ~20-30 degree skew and small
        /// 2px rounded corners. It is much shorter than a tab and sits on (or 1px above) the
        /// toolbar line. See the reference screenshot: the little stub after "New Tab" leans
        /// right, it is NOT a mini-tab.
        /// </para>
        /// </summary>
        public static GraphicsPath CreateNewTabButtonPath(RectangleF bounds, float scale)
        {
            float c = Chrome60Metrics.CornerSize * scale;

            float h = bounds.Height;
            if (h < 1f) h = 1f;

            // Same slope as a tab so the button's left edge tucks parallel under the last
            // tab's right slope: skew = height * (endcap / tabHeight).
            float skew = h * Chrome60Metrics.TabEndcapWidth / Chrome60Metrics.TabHeight;
            float maxSkew = bounds.Width - 4f * c - 2f;
            if (maxSkew < 0f) maxSkew = 0f;
            if (skew > maxSkew) skew = maxSkew;

            float l = bounds.Left;
            float r = bounds.Right;
            float t = bounds.Top;
            float b = bounds.Bottom;

            // Sharp corners of the parallelogram (bottom shifted right vs top).
            var TL = new PointF(l, t);
            var TR = new PointF(r - skew, t);
            var BR = new PointF(r, b);
            var BL = new PointF(l + skew, b);

            // Unit vector down-right along the slanted edges.
            float slantLen = (float)System.Math.Sqrt(skew * skew + h * h);
            if (slantLen < 0.001f) slantLen = 0.001f;
            float ux = skew / slantLen;
            float uy = h / slantLen;

            // Inset each corner by c along both adjacent edges for a 2px round.
            var TL_top = new PointF(TL.X + c, TL.Y);
            var TL_left = new PointF(TL.X + ux * c, TL.Y + uy * c);

            var TR_top = new PointF(TR.X - c, TR.Y);
            var TR_right = new PointF(TR.X + ux * c, TR.Y + uy * c);

            var BR_bottom = new PointF(BR.X - c, BR.Y);
            var BR_right = new PointF(BR.X - ux * c, BR.Y - uy * c);

            var BL_bottom = new PointF(BL.X + c, BL.Y);
            var BL_left = new PointF(BL.X - ux * c, BL.Y - uy * c);

            var path = new GraphicsPath();
            path.StartFigure();

            path.AddLine(TL_top, TR_top);
            AddQuad(path, TR_top, TR, TR_right);
            path.AddLine(TR_right, BR_right);
            AddQuad(path, BR_right, BR, BR_bottom);
            path.AddLine(BR_bottom, BL_bottom);
            AddQuad(path, BL_bottom, BL, BL_left);
            path.AddLine(BL_left, TL_left);
            AddQuad(path, TL_left, TL, TL_top);
            path.CloseFigure();

            return path;
        }

        /// <summary>
        /// Returns the rectangle to use for a crisp 1-device-pixel stroke of a shape whose fill
        /// occupies <paramref name="fillBounds"/>: inset by half a pixel on the left, top and right.
        /// The bottom is left untouched so tab strokes end exactly on the toolbar line.
        /// </summary>
        public static RectangleF StrokeBoundsForTab(Rectangle fillBounds)
        {
            return new RectangleF(fillBounds.X + 0.5f, fillBounds.Y + 0.5f, fillBounds.Width - 1f, fillBounds.Height - 0.5f);
        }

        /// <summary>Inset by half a pixel on all sides for closed shapes such as the new tab button.</summary>
        public static RectangleF StrokeBoundsForClosedShape(Rectangle fillBounds)
        {
            return new RectangleF(fillBounds.X + 0.5f, fillBounds.Y + 0.5f, fillBounds.Width - 1f, fillBounds.Height - 1f);
        }

        /// <summary>Adds a quadratic Bezier (p0 -> p2 with control q) as an equivalent cubic segment.</summary>
        private static void AddQuad(GraphicsPath path, PointF p0, PointF q, PointF p2)
        {
            var c1 = new PointF(p0.X + 2f / 3f * (q.X - p0.X), p0.Y + 2f / 3f * (q.Y - p0.Y));
            var c2 = new PointF(p2.X + 2f / 3f * (q.X - p2.X), p2.Y + 2f / 3f * (q.Y - p2.Y));
            path.AddBezier(p0, c1, c2, p2);
        }
    }
}