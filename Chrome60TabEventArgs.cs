using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Chrome60Tabs
{
    /// <summary>Which part of the tab strip a point falls on.</summary>
    public enum Chrome60HitArea
    {
        /// <summary>Outside the control.</summary>
        None,
        /// <summary>Empty tab strip background (Chrome: double-click opens a new tab; drag moves the window).</summary>
        TabStrip,
        /// <summary>The body of a tab (sloped edges and top included, per the real silhouette).</summary>
        Tab,
        /// <summary>The favicon of a tab.</summary>
        TabIcon,
        /// <summary>The title text of a tab.</summary>
        TabText,
        /// <summary>The close box of a tab.</summary>
        TabCloseButton,
        /// <summary>The new tab button.</summary>
        NewTabButton
    }

    /// <summary>How tab widths are computed.</summary>
    public enum Chrome60TabSizingMode
    {
        /// <summary>Chrome behaviour: tabs share the strip, shrinking from StandardTabWidth down to the minimums.</summary>
        Dynamic,
        /// <summary>Every tab gets <see cref="Chrome60TabControl.FixedTabWidth"/>.</summary>
        Fixed
    }

    /// <summary>Visual state of a small button (close box, new tab button).</summary>
    public enum Chrome60ButtonState
    {
        Normal,
        Hot,
        Pressed
    }

    /// <summary>Result of <see cref="Chrome60TabControl.HitTest(Point)"/>.</summary>
    public struct Chrome60HitTestInfo
    {
        public Chrome60HitTestInfo(Chrome60HitArea area, Chrome60Tab tab, int index)
        {
            Area = area;
            Tab = tab;
            Index = index;
        }

        public Chrome60HitArea Area { get; private set; }
        public Chrome60Tab Tab { get; private set; }
        public int Index { get; private set; }

        public bool IsOnTab
        {
            get
            {
                return Area == Chrome60HitArea.Tab || Area == Chrome60HitArea.TabIcon ||
                       Area == Chrome60HitArea.TabText || Area == Chrome60HitArea.TabCloseButton;
            }
        }
    }

    public class Chrome60TabEventArgs : EventArgs
    {
        public Chrome60TabEventArgs(Chrome60Tab tab, int index)
        {
            Tab = tab;
            Index = index;
        }

        public Chrome60Tab Tab { get; private set; }
        public int Index { get; private set; }
    }

    public class Chrome60TabCancelEventArgs : CancelEventArgs
    {
        public Chrome60TabCancelEventArgs(Chrome60Tab tab, int index)
        {
            Tab = tab;
            Index = index;
        }

        public Chrome60Tab Tab { get; private set; }
        public int Index { get; private set; }
    }

    public class Chrome60SelectedTabChangedEventArgs : EventArgs
    {
        public Chrome60SelectedTabChangedEventArgs(Chrome60Tab oldTab, int oldIndex, Chrome60Tab newTab, int newIndex)
        {
            OldTab = oldTab;
            OldIndex = oldIndex;
            NewTab = newTab;
            NewIndex = newIndex;
        }

        public Chrome60Tab OldTab { get; private set; }
        public int OldIndex { get; private set; }
        public Chrome60Tab NewTab { get; private set; }
        public int NewIndex { get; private set; }
    }

    public class Chrome60TabMovedEventArgs : EventArgs
    {
        public Chrome60TabMovedEventArgs(Chrome60Tab tab, int oldIndex, int newIndex)
        {
            Tab = tab;
            OldIndex = oldIndex;
            NewIndex = newIndex;
        }

        public Chrome60Tab Tab { get; private set; }
        public int OldIndex { get; private set; }
        public int NewIndex { get; private set; }
    }

    public class Chrome60TabMouseEventArgs : MouseEventArgs
    {
        public Chrome60TabMouseEventArgs(MouseEventArgs e, Chrome60HitTestInfo hit)
            : base(e.Button, e.Clicks, e.X, e.Y, e.Delta)
        {
            Hit = hit;
        }

        public Chrome60HitTestInfo Hit { get; private set; }
        public Chrome60Tab Tab { get { return Hit.Tab; } }
        public int Index { get { return Hit.Index; } }
        public Chrome60HitArea Area { get { return Hit.Area; } }
    }

    /// <summary>
    /// Raised when a tab is dragged far enough vertically to be torn off. Cancel to keep the tab;
    /// otherwise the tab is removed from the control and the handler is expected to place
    /// it elsewhere (typically a new window).
    /// </summary>
    public class Chrome60TabDetachEventArgs : CancelEventArgs
    {
        public Chrome60TabDetachEventArgs(Chrome60Tab tab, int index, Point screenLocation)
        {
            Tab = tab;
            Index = index;
            ScreenLocation = screenLocation;
        }

        public Chrome60Tab Tab { get; private set; }
        public int Index { get; private set; }
        public Point ScreenLocation { get; private set; }
    }
}
