using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Chrome60Tabs
{
    /// <summary>
    /// Resolved geometry for one tab, in device pixels. Produced by the control's layout pass and
    /// consumed by the renderer and hit-testing. Paths are owned by this object.
    /// </summary>
    public sealed class Chrome60TabLayout : IDisposable
    {
        public Chrome60Tab Tab { get; internal set; }
        public int Index { get; internal set; }

        /// <summary>Full bounding box of the tab silhouette (overlaps neighbours by the end cap).</summary>
        public Rectangle Bounds { get; internal set; }

        public Rectangle IconBounds { get; internal set; }
        public Rectangle TextBounds { get; internal set; }
        public Rectangle CloseButtonBounds { get; internal set; }
        public Rectangle CloseButtonHitBounds { get; internal set; }

        public bool ShowIcon { get; internal set; }
        public bool ShowCloseButton { get; internal set; }
        public bool IsActive { get; internal set; }
        public bool IsDragging { get; internal set; }

        /// <summary>Closed fill / hit-test silhouette.</summary>
        public GraphicsPath FillPath { get; internal set; }

        /// <summary>Open outline positioned for a crisp 1-pixel stroke.</summary>
        public GraphicsPath StrokePath { get; internal set; }

        public void Dispose()
        {
            if (FillPath != null) { FillPath.Dispose(); FillPath = null; }
            if (StrokePath != null) { StrokePath.Dispose(); StrokePath = null; }
        }
    }
}
