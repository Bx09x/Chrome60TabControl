using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Chrome60Tabs.Rendering;
using FormsTimer = System.Windows.Forms.Timer;

namespace Chrome60Tabs
{
    /// <summary>
    /// A reusable Windows Forms tab strip that reproduces the Google Chrome 60 (2017) tab appearance:
    /// trapezoid tabs with crossing sloped edges, the active tab merging into the toolbar, favicon,
    /// faded title, Material close box and the skewed-parallelogram new tab button tucked behind
    /// the last tab.
    /// <para>
    /// The control only renders the strip. Page content is optional: assign a <see cref="ContentHost"/>
    /// (any container control) and each tab's <see cref="Chrome60Tab.Content"/> is docked into it and
    /// shown when the tab is selected.
    /// </para>
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("SelectedIndexChanged")]
    [DefaultProperty("Tabs")]
    [Description("A tab strip that reproduces the Google Chrome 60 (2017) tab appearance.")]
    public class Chrome60TabControl : Control
    {
        private const int WM_CONTEXTMENU = 0x007B;

        private readonly Chrome60TabCollection _tabs;
        private readonly List<Chrome60TabLayout> _layouts = new List<Chrome60TabLayout>();
        private readonly FormsTimer _animationTimer;
        private readonly ToolTip _toolTip;

        private Chrome60TabRenderer _renderer = new Chrome60TabRenderer();

        private int _selectedIndex = -1;
        private bool _layoutValid;
        private Rectangle _newTabButtonBounds;
        private GraphicsPath _newTabButtonPath;

        // Appearance
        private Color _activeTabColor = Chrome60Colors.Toolbar;
        private Color _inactiveTabColor = Chrome60Colors.InactiveTab;
        private Color _inactiveTabHoverColor = Chrome60Colors.InactiveTabHover;
        private Color _tabBorderColor = Chrome60Colors.Stroke;
        private Color _tabTextColor = Chrome60Colors.TabText;
        private Color _inactiveTabTextColor = Chrome60Colors.InactiveTabText;
        private Color _disabledTabTextColor = Chrome60Colors.DisabledTabText;
        private Color _closeButtonColor = Chrome60Colors.CloseButton;
        private Color _inactiveCloseButtonColor = Chrome60Colors.InactiveCloseButton;
        private Color _closeButtonHoverColor = Chrome60Colors.CloseButtonHover;
        private Color _closeButtonPressedColor = Chrome60Colors.CloseButtonPressed;
        private Color _closeButtonHoverGlyphColor = Chrome60Colors.CloseButtonGlyphOnHover;
        private Color _newTabButtonColor = Chrome60Colors.NewTabButton;
        private Color _newTabButtonHoverColor = Chrome60Colors.NewTabButtonHover;
        private Color _newTabButtonPressedColor = Chrome60Colors.NewTabButtonPressed;
        private Color _newTabButtonGlyphColor = Chrome60Colors.NewTabButtonGlyph;
        private Color _defaultIconColor = Chrome60Colors.DefaultFavicon;

        // Behaviour / sizing
        private Chrome60TabSizingMode _sizingMode = Chrome60TabSizingMode.Dynamic;
        private int _fixedTabWidth = Chrome60Metrics.StandardTabWidth;
        private int _standardTabWidth = Chrome60Metrics.StandardTabWidth;
        private int _minimumInactiveTabWidth = Chrome60Metrics.MinimumInactiveTabWidth;
        private int _minimumActiveTabWidth = Chrome60Metrics.MinimumActiveTabWidth;
        private int _topPadding = Chrome60Metrics.DefaultTopPadding;
        private int _leftPadding;
        private int _rightPadding;
        private bool _autoHeight = true;
        private bool _showCloseButtons = true;
        private bool _showIcons = true;
        private bool _showDefaultIcon = true;
        private bool _showNewTabButton = true;
        private bool _showNewTabButtonPlus;
        private bool _allowTabReorder = true;
        private bool _allowTabDetach;
        private bool _showToolTips = true;
        private bool _closeOnMiddleClick = true;
        private bool _newTabOnDoubleClick = true;
        private int _hoverAnimationDuration = 200;
        private ContextMenuStrip _tabContextMenuStrip;
        private Control _contentHost;
        private bool _fontIsDefault = true;

        // Interaction state
        private Chrome60Tab _hoveredTab;
        private Chrome60HitArea _hoveredArea = Chrome60HitArea.None;
        private bool _newTabHovered;
        private bool _newTabPressed;
        private float _newTabHoverProgress;
        private Chrome60Tab _pressedTab;
        private Chrome60HitArea _pressedArea = Chrome60HitArea.None;
        private Point _pressLocation;
        private MouseButtons _pressButton;
        private Chrome60Tab _dragTab;
        private int _dragOffsetX;
        private int _dragX;
        private DateTime _lastAnimationTick;
        private string _currentToolTip;
        private Chrome60Tab _contextMenuTab;

        // Scrolling (overflow): tabs shrink to their minimums, then the strip scrolls
        // instead of shrinking further. The new-tab button stays pinned at the right.
        private bool _allowTabScroll = true;
        private int _scrollOffset;      // device pixels scrolled to the left
        private int _maxScroll;         // device pixels of total overflow (last layout)
        private Rectangle _tabViewport; // device pixels tabs may paint in (last layout)
        private bool _isScrolling;      // true when the last layout overflowed

        public Chrome60TabControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.StandardClick |
                     ControlStyles.StandardDoubleClick |
                     ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.ContainerControl, false);

            _tabs = new Chrome60TabCollection(this);
            TabStop = true;
            AccessibleRole = AccessibleRole.PageTabList;
            base.BackColor = Chrome60Colors.Frame;
            base.Font = CreateDefaultTabFont();

            _animationTimer = new FormsTimer { Interval = 16 };
            _animationTimer.Tick += OnAnimationTick;

            _toolTip = new ToolTip { ShowAlways = true, InitialDelay = 700, ReshowDelay = 200 };

            Height = PreferredHeight;
        }

        #region Events

        [Category("Action")]
        [Description("Raised when the user clicks the new tab button, double-clicks the empty strip or presses Ctrl+T.")]
        public event EventHandler NewTabRequested;

        [Category("Behavior")]
        [Description("Raised before the selected tab changes. Cancel to keep the current tab.")]
        public event EventHandler<Chrome60TabCancelEventArgs> Selecting;

        [Category("Behavior")]
        public event EventHandler<Chrome60SelectedTabChangedEventArgs> SelectedIndexChanged;

        [Category("Behavior")]
        [Description("Raised when the user asks to close a tab (close box, middle click, Ctrl+W). Cancel to keep it open.")]
        public event EventHandler<Chrome60TabCancelEventArgs> TabClosing;

        [Category("Behavior")]
        [Description("Raised after a tab has been closed through the UI or CloseTab.")]
        public event EventHandler<Chrome60TabEventArgs> TabClosed;

        [Category("Behavior")]
        public event EventHandler<Chrome60TabEventArgs> TabAdded;

        [Category("Behavior")]
        public event EventHandler<Chrome60TabEventArgs> TabRemoved;

        [Category("Behavior")]
        public event EventHandler<Chrome60TabMovedEventArgs> TabMoved;

        [Category("Behavior")]
        [Description("Raised when a tab is dragged out of the strip (AllowTabDetach). Cancel to keep it.")]
        public event EventHandler<Chrome60TabDetachEventArgs> TabDetachRequested;

        [Category("Mouse")]
        public event EventHandler<Chrome60TabMouseEventArgs> TabMouseClick;

        [Category("Mouse")]
        public event EventHandler<Chrome60TabMouseEventArgs> TabMouseDoubleClick;

        [Category("Behavior")]
        public event EventHandler HoveredTabChanged;

        #endregion

        #region Collection / selection API

        [Category("Data")]
        [Description("The tabs shown in the strip.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Editor("System.ComponentModel.Design.CollectionEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(UITypeEditor))]
        public Chrome60TabCollection Tabs
        {
            get { return _tabs; }
        }

        [Category("Behavior")]
        [DefaultValue(-1)]
        [Description("Index of the selected tab, or -1.")]
        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set { SelectTabCore(value, true); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Chrome60Tab SelectedTab
        {
            get { return _selectedIndex >= 0 && _selectedIndex < _tabs.Count ? _tabs[_selectedIndex] : null; }
            set { SelectedIndex = value == null ? -1 : _tabs.IndexOf(value); }
        }

        [Browsable(false)]
        public Chrome60Tab HoveredTab
        {
            get { return _hoveredTab; }
        }

        /// <summary>The tab the <see cref="TabContextMenuStrip"/> was opened for.</summary>
        [Browsable(false)]
        public Chrome60Tab ContextMenuTab
        {
            get { return _contextMenuTab; }
        }

        [Browsable(false)]
        public int TabCount
        {
            get { return _tabs.Count; }
        }

        public Chrome60Tab AddTab(string text)
        {
            return _tabs.Add(text);
        }

        public Chrome60Tab AddTab(string text, Image icon)
        {
            return _tabs.Add(text, icon);
        }

        public Chrome60Tab AddTab(string text, Image icon, Control content)
        {
            return _tabs.Add(text, icon, content);
        }

        public void AddTab(Chrome60Tab tab)
        {
            _tabs.Add(tab);
        }

        public void InsertTab(int index, Chrome60Tab tab)
        {
            _tabs.Insert(index, tab);
        }

        /// <summary>Removes a tab without raising TabClosing / TabClosed.</summary>
        public bool RemoveTab(Chrome60Tab tab)
        {
            return _tabs.Remove(tab);
        }

        public void RemoveTabAt(int index)
        {
            _tabs.RemoveAt(index);
        }

        /// <summary>
        /// Closes a tab the way the UI does: raises <see cref="TabClosing"/> (cancellable), removes the
        /// tab, then raises <see cref="TabClosed"/>. Returns false when cancelled.
        /// </summary>
        public bool CloseTab(Chrome60Tab tab)
        {
            int index = _tabs.IndexOf(tab);
            if (index < 0) return false;

            var cancelArgs = new Chrome60TabCancelEventArgs(tab, index);
            OnTabClosing(cancelArgs);
            if (cancelArgs.Cancel) return false;

            _tabs.RemoveAt(index);
            OnTabClosed(new Chrome60TabEventArgs(tab, index));
            return true;
        }

        public bool CloseTabAt(int index)
        {
            if (index < 0 || index >= _tabs.Count) throw new ArgumentOutOfRangeException("index");
            return CloseTab(_tabs[index]);
        }

        /// <summary>Moves a tab to a new position without changing which tab is selected.</summary>
        public void MoveTab(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _tabs.Count) throw new ArgumentOutOfRangeException("fromIndex");
            if (toIndex < 0 || toIndex >= _tabs.Count) throw new ArgumentOutOfRangeException("toIndex");
            if (fromIndex == toIndex) return;

            var selected = SelectedTab;
            var tab = _tabs[fromIndex];

            _tabs.SuppressOwnerNotifications = true;
            try
            {
                _tabs.RemoveAt(fromIndex);
                _tabs.Insert(toIndex, tab);
            }
            finally
            {
                _tabs.SuppressOwnerNotifications = false;
            }

            _selectedIndex = selected == null ? -1 : _tabs.IndexOf(selected);
            InvalidateLayout();
            OnTabMoved(new Chrome60TabMovedEventArgs(tab, fromIndex, toIndex));
        }

        public void MoveTab(Chrome60Tab tab, int toIndex)
        {
            int from = _tabs.IndexOf(tab);
            if (from < 0) throw new ArgumentException("The tab does not belong to this control.", "tab");
            MoveTab(from, toIndex);
        }

        public void SelectNextTab()
        {
            SelectAdjacent(1, true);
        }

        public void SelectPreviousTab()
        {
            SelectAdjacent(-1, true);
        }

        #endregion

        #region Appearance properties

        [Category("Chrome 60 Colors")]
        [Description("Fill of the selected tab. Chrome 60 uses the toolbar colour so tab and toolbar merge.")]
        public Color ActiveTabColor { get { return _activeTabColor; } set { SetColor(ref _activeTabColor, value); } }
        private bool ShouldSerializeActiveTabColor() { return _activeTabColor != Chrome60Colors.Toolbar; }
        private void ResetActiveTabColor() { ActiveTabColor = Chrome60Colors.Toolbar; }

        [Category("Chrome 60 Colors")]
        public Color InactiveTabColor { get { return _inactiveTabColor; } set { SetColor(ref _inactiveTabColor, value); } }
        private bool ShouldSerializeInactiveTabColor() { return _inactiveTabColor != Chrome60Colors.InactiveTab; }
        private void ResetInactiveTabColor() { InactiveTabColor = Chrome60Colors.InactiveTab; }

        [Category("Chrome 60 Colors")]
        public Color InactiveTabHoverColor { get { return _inactiveTabHoverColor; } set { SetColor(ref _inactiveTabHoverColor, value); } }
        private bool ShouldSerializeInactiveTabHoverColor() { return _inactiveTabHoverColor != Chrome60Colors.InactiveTabHover; }
        private void ResetInactiveTabHoverColor() { InactiveTabHoverColor = Chrome60Colors.InactiveTabHover; }

        [Category("Chrome 60 Colors")]
        [Description("Stroke of tabs, the new tab button and the toolbar top line.")]
        public Color TabBorderColor { get { return _tabBorderColor; } set { SetColor(ref _tabBorderColor, value); } }
        private bool ShouldSerializeTabBorderColor() { return _tabBorderColor != Chrome60Colors.Stroke; }
        private void ResetTabBorderColor() { TabBorderColor = Chrome60Colors.Stroke; }

        [Category("Chrome 60 Colors")]
        public Color TabTextColor { get { return _tabTextColor; } set { SetColor(ref _tabTextColor, value); } }
        private bool ShouldSerializeTabTextColor() { return _tabTextColor != Chrome60Colors.TabText; }
        private void ResetTabTextColor() { TabTextColor = Chrome60Colors.TabText; }

        [Category("Chrome 60 Colors")]
        public Color InactiveTabTextColor { get { return _inactiveTabTextColor; } set { SetColor(ref _inactiveTabTextColor, value); } }
        private bool ShouldSerializeInactiveTabTextColor() { return _inactiveTabTextColor != Chrome60Colors.InactiveTabText; }
        private void ResetInactiveTabTextColor() { InactiveTabTextColor = Chrome60Colors.InactiveTabText; }

        [Category("Chrome 60 Colors")]
        public Color DisabledTabTextColor { get { return _disabledTabTextColor; } set { SetColor(ref _disabledTabTextColor, value); } }
        private bool ShouldSerializeDisabledTabTextColor() { return _disabledTabTextColor != Chrome60Colors.DisabledTabText; }
        private void ResetDisabledTabTextColor() { DisabledTabTextColor = Chrome60Colors.DisabledTabText; }

        [Category("Chrome 60 Colors")]
        public Color CloseButtonColor { get { return _closeButtonColor; } set { SetColor(ref _closeButtonColor, value); } }
        private bool ShouldSerializeCloseButtonColor() { return _closeButtonColor != Chrome60Colors.CloseButton; }
        private void ResetCloseButtonColor() { CloseButtonColor = Chrome60Colors.CloseButton; }

        [Category("Chrome 60 Colors")]
        public Color InactiveCloseButtonColor { get { return _inactiveCloseButtonColor; } set { SetColor(ref _inactiveCloseButtonColor, value); } }
        private bool ShouldSerializeInactiveCloseButtonColor() { return _inactiveCloseButtonColor != Chrome60Colors.InactiveCloseButton; }
        private void ResetInactiveCloseButtonColor() { InactiveCloseButtonColor = Chrome60Colors.InactiveCloseButton; }

        [Category("Chrome 60 Colors")]
        [Description("Red circle behind the close box while hovered.")]
        public Color CloseButtonHoverColor { get { return _closeButtonHoverColor; } set { SetColor(ref _closeButtonHoverColor, value); } }
        private bool ShouldSerializeCloseButtonHoverColor() { return _closeButtonHoverColor != Chrome60Colors.CloseButtonHover; }
        private void ResetCloseButtonHoverColor() { CloseButtonHoverColor = Chrome60Colors.CloseButtonHover; }

        [Category("Chrome 60 Colors")]
        public Color CloseButtonPressedColor { get { return _closeButtonPressedColor; } set { SetColor(ref _closeButtonPressedColor, value); } }
        private bool ShouldSerializeCloseButtonPressedColor() { return _closeButtonPressedColor != Chrome60Colors.CloseButtonPressed; }
        private void ResetCloseButtonPressedColor() { CloseButtonPressedColor = Chrome60Colors.CloseButtonPressed; }

        [Category("Chrome 60 Colors")]
        public Color CloseButtonHoverGlyphColor { get { return _closeButtonHoverGlyphColor; } set { SetColor(ref _closeButtonHoverGlyphColor, value); } }
        private bool ShouldSerializeCloseButtonHoverGlyphColor() { return _closeButtonHoverGlyphColor != Chrome60Colors.CloseButtonGlyphOnHover; }
        private void ResetCloseButtonHoverGlyphColor() { CloseButtonHoverGlyphColor = Chrome60Colors.CloseButtonGlyphOnHover; }

        [Category("Chrome 60 Colors")]
        public Color NewTabButtonColor { get { return _newTabButtonColor; } set { SetColor(ref _newTabButtonColor, value); } }
        private bool ShouldSerializeNewTabButtonColor() { return _newTabButtonColor != Chrome60Colors.NewTabButton; }
        private void ResetNewTabButtonColor() { NewTabButtonColor = Chrome60Colors.NewTabButton; }

        [Category("Chrome 60 Colors")]
        public Color NewTabButtonHoverColor { get { return _newTabButtonHoverColor; } set { SetColor(ref _newTabButtonHoverColor, value); } }
        private bool ShouldSerializeNewTabButtonHoverColor() { return _newTabButtonHoverColor != Chrome60Colors.NewTabButtonHover; }
        private void ResetNewTabButtonHoverColor() { NewTabButtonHoverColor = Chrome60Colors.NewTabButtonHover; }

        [Category("Chrome 60 Colors")]
        public Color NewTabButtonPressedColor { get { return _newTabButtonPressedColor; } set { SetColor(ref _newTabButtonPressedColor, value); } }
        private bool ShouldSerializeNewTabButtonPressedColor() { return _newTabButtonPressedColor != Chrome60Colors.NewTabButtonPressed; }
        private void ResetNewTabButtonPressedColor() { NewTabButtonPressedColor = Chrome60Colors.NewTabButtonPressed; }

        [Category("Chrome 60 Colors")]
        public Color NewTabButtonGlyphColor { get { return _newTabButtonGlyphColor; } set { SetColor(ref _newTabButtonGlyphColor, value); } }
        private bool ShouldSerializeNewTabButtonGlyphColor() { return _newTabButtonGlyphColor != Chrome60Colors.NewTabButtonGlyph; }
        private void ResetNewTabButtonGlyphColor() { NewTabButtonGlyphColor = Chrome60Colors.NewTabButtonGlyph; }

        [Category("Chrome 60 Colors")]
        public Color DefaultIconColor { get { return _defaultIconColor; } set { SetColor(ref _defaultIconColor, value); } }
        private bool ShouldSerializeDefaultIconColor() { return _defaultIconColor != Chrome60Colors.DefaultFavicon; }
        private void ResetDefaultIconColor() { DefaultIconColor = Chrome60Colors.DefaultFavicon; }

        /// <summary>Background of the strip (the browser frame colour). Alias of <see cref="Control.BackColor"/>.</summary>
        [Category("Chrome 60 Colors")]
        public Color TabStripColor
        {
            get { return BackColor; }
            set { BackColor = value; }
        }
        private bool ShouldSerializeTabStripColor() { return false; }

        public override Color BackColor
        {
            get { return base.BackColor; }
            set { base.BackColor = value; }
        }
        private bool ShouldSerializeBackColor() { return BackColor != Chrome60Colors.Frame; }
        public override void ResetBackColor() { BackColor = Chrome60Colors.Frame; }

        /// <summary>Title font. Defaults to Segoe UI 9pt, the Windows UI font Chrome 60 used for tab titles.</summary>
        public override Font Font
        {
            get { return base.Font; }
            set
            {
                _fontIsDefault = false;
                base.Font = value;
            }
        }
        private bool ShouldSerializeFont() { return !_fontIsDefault; }
        public override void ResetFont()
        {
            base.Font = CreateDefaultTabFont();
            _fontIsDefault = true;
        }

        /// <summary>Renderer used to paint the strip. Replace with a subclass to customise drawing.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Chrome60TabRenderer Renderer
        {
            get { return _renderer; }
            set
            {
                _renderer = value ?? new Chrome60TabRenderer();
                Invalidate();
            }
        }

        #endregion

        #region Behaviour / layout properties

        [Category("Chrome 60 Layout")]
        [DefaultValue(Chrome60TabSizingMode.Dynamic)]
        public Chrome60TabSizingMode SizingMode
        {
            get { return _sizingMode; }
            set { if (_sizingMode != value) { _sizingMode = value; InvalidateLayout(); } }
        }

        [Category("Chrome 60 Layout")]
        [DefaultValue(Chrome60Metrics.StandardTabWidth)]
        [Description("Tab width (logical pixels) used in Fixed sizing mode.")]
        public int FixedTabWidth
        {
            get { return _fixedTabWidth; }
            set { SetInt(ref _fixedTabWidth, Math.Max(Chrome60Metrics.TabEndcapWidth * 2 + 4, value)); }
        }

        [Category("Chrome 60 Layout")]
        [DefaultValue(Chrome60Metrics.StandardTabWidth)]
        [Description("Maximum (standard) tab width in logical pixels. Chrome 60: 192.")]
        public int StandardTabWidth
        {
            get { return _standardTabWidth; }
            set { SetInt(ref _standardTabWidth, Math.Max(_minimumActiveTabWidth, value)); }
        }

        [Category("Chrome 60 Layout")]
        [DefaultValue(Chrome60Metrics.MinimumInactiveTabWidth)]
        public int MinimumInactiveTabWidth
        {
            get { return _minimumInactiveTabWidth; }
            set { SetInt(ref _minimumInactiveTabWidth, Math.Max(Chrome60Metrics.TabEndcapWidth * 2 + 4, value)); }
        }

        [Category("Chrome 60 Layout")]
        [DefaultValue(Chrome60Metrics.MinimumActiveTabWidth)]
        public int MinimumActiveTabWidth
        {
            get { return _minimumActiveTabWidth; }
            set { SetInt(ref _minimumActiveTabWidth, Math.Max(_minimumInactiveTabWidth, value)); }
        }

        [Category("Chrome 60 Layout")]
        [DefaultValue(Chrome60Metrics.DefaultTopPadding)]
        [Description("Logical pixels of frame shown above the tabs.")]
        public int TopPadding
        {
            get { return _topPadding; }
            set
            {
                value = Math.Max(0, value);
                if (_topPadding == value) return;
                _topPadding = value;
                ApplyAutoHeight();
                InvalidateLayout();
            }
        }

        [Category("Chrome 60 Layout")]
        [DefaultValue(0)]
        public int LeftPadding
        {
            get { return _leftPadding; }
            set { SetInt(ref _leftPadding, Math.Max(0, value)); }
        }

        [Category("Chrome 60 Layout")]
        [DefaultValue(0)]
        public int RightPadding
        {
            get { return _rightPadding; }
            set { SetInt(ref _rightPadding, Math.Max(0, value)); }
        }

        [Category("Chrome 60 Layout")]
        [DefaultValue(true)]
        [Description("Keep the control exactly as tall as the Chrome 60 strip at the current DPI.")]
        public bool AutoHeight
        {
            get { return _autoHeight; }
            set
            {
                if (_autoHeight == value) return;
                _autoHeight = value;
                ApplyAutoHeight();
            }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(true)]
        public bool ShowCloseButtons
        {
            get { return _showCloseButtons; }
            set { SetBool(ref _showCloseButtons, value, true); }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(true)]
        public bool ShowIcons
        {
            get { return _showIcons; }
            set { SetBool(ref _showIcons, value, true); }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(true)]
        [Description("Draw the grey default page glyph for tabs without an Icon.")]
        public bool ShowDefaultIcon
        {
            get { return _showDefaultIcon; }
            set { SetBool(ref _showDefaultIcon, value, false); }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(true)]
        public bool ShowNewTabButton
        {
            get { return _showNewTabButton; }
            set { SetBool(ref _showNewTabButton, value, true); }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(false)]
        [Description("Draw a plus glyph on the new tab button. Chrome 60 desktop drew a plain skewed parallelogram; the plus only appeared in touch/hybrid mode.")]
        public bool ShowNewTabButtonPlus
        {
            get { return _showNewTabButtonPlus; }
            set { SetBool(ref _showNewTabButtonPlus, value, false); }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(true)]
        public bool AllowTabReorder
        {
            get { return _allowTabReorder; }
            set { _allowTabReorder = value; }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(false)]
        [Description("Dragging a tab far enough vertically raises TabDetachRequested and removes it.")]
        public bool AllowTabDetach
        {
            get { return _allowTabDetach; }
            set { _allowTabDetach = value; }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(true)]
        public bool ShowToolTips
        {
            get { return _showToolTips; }
            set
            {
                _showToolTips = value;
                if (!value) SetToolTip(null);
            }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(true)]
        public bool CloseOnMiddleClick
        {
            get { return _closeOnMiddleClick; }
            set { _closeOnMiddleClick = value; }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(true)]
        [Description("Double-clicking the empty strip raises NewTabRequested, like Chrome.")]
        public bool NewTabOnDoubleClick
        {
            get { return _newTabOnDoubleClick; }
            set { _newTabOnDoubleClick = value; }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(true)]
        [Description("When tabs at their minimum widths still overflow, scroll the strip instead of shrinking further. The new-tab button stays pinned at the right.")]
        public bool AllowTabScroll
        {
            get { return _allowTabScroll; }
            set
            {
                if (_allowTabScroll == value) return;
                _allowTabScroll = value;
                _scrollOffset = 0;
                InvalidateLayout();
            }
        }

        /// <summary>True when the last layout overflowed and the strip is scrolling (tabs clipped, button pinned).</summary>
        [Browsable(false)]
        public bool IsScrolling
        {
            get { EnsureLayout(); return _isScrolling; }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(200)]
        [Description("Duration of the inactive tab hover glow fade in milliseconds. 0 disables the animation.")]
        public int HoverAnimationDuration
        {
            get { return _hoverAnimationDuration; }
            set { _hoverAnimationDuration = Math.Max(0, value); }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(null)]
        [Description("Context menu shown when right-clicking a tab. ContextMenuTab tells you which one.")]
        public ContextMenuStrip TabContextMenuStrip
        {
            get { return _tabContextMenuStrip; }
            set { _tabContextMenuStrip = value; }
        }

        [Category("Chrome 60 Behavior")]
        [DefaultValue(null)]
        [Description("Container that receives each tab's Content control. Only the selected tab's content is visible.")]
        public Control ContentHost
        {
            get { return _contentHost; }
            set
            {
                if (ReferenceEquals(_contentHost, value)) return;

                if (_contentHost != null)
                    foreach (var tab in _tabs) DetachContent(tab, tab.Content);

                _contentHost = value;

                if (_contentHost != null)
                {
                    foreach (var tab in _tabs) AttachContent(tab);
                    UpdateContentVisibility();
                }
            }
        }

        /// <summary>Height the control wants at the current DPI: TopPadding + tab height.</summary>
        [Browsable(false)]
        public int PreferredHeight
        {
            get { return Round((_topPadding + Chrome60Metrics.TabHeight) * Scale); }
        }

        /// <summary>DPI scale factor (1.0 at 96 DPI).</summary>
        [Browsable(false)]
        public float Scale
        {
            get { return DeviceDpi / 96f; }
        }

        [Browsable(false)]
        public Rectangle NewTabButtonBounds
        {
            get { EnsureLayout(); return _showNewTabButton ? _newTabButtonBounds : Rectangle.Empty; }
        }

        protected override Size DefaultSize
        {
            get { return new Size(600, Chrome60Metrics.DefaultTopPadding + Chrome60Metrics.TabHeight); }
        }

        #endregion

        #region Public geometry API

        /// <summary>Scrolls the strip so the tab at <paramref name="index"/> is fully visible. No-op when not scrolling.</summary>
        public void EnsureTabVisible(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;
            EnsureLayout();
            if (!_isScrolling) return;

            var bounds = GetTabBounds(_tabs[index]);
            if (bounds == Rectangle.Empty) return;

            int viewLeft = _tabViewport.Left;
            int viewRight = _tabViewport.Right;
            int newOffset = _scrollOffset;
            if (bounds.Left < viewLeft) newOffset -= viewLeft - bounds.Left;
            else if (bounds.Right > viewRight) newOffset += bounds.Right - viewRight;
            SetScrollOffset(newOffset);
        }

        /// <summary>Scrolls the strip so <paramref name="tab"/> is fully visible. No-op when not scrolling.</summary>
        public void EnsureTabVisible(Chrome60Tab tab)
        {
            if (tab == null) return;
            EnsureTabVisible(_tabs.IndexOf(tab));
        }

        /// <summary>Scrolls by <paramref name="deltaPixels"/> device pixels (positive = toward the end). Clamped.</summary>
        public void ScrollBy(int deltaPixels)
        {
            if (deltaPixels == 0) return;
            SetScrollOffset(_scrollOffset + deltaPixels);
        }

        /// <summary>Scrolls to the start of the strip.</summary>
        public void ScrollToStart()
        {
            SetScrollOffset(0);
        }

        /// <summary>Scrolls to the end of the strip.</summary>
        public void ScrollToEnd()
        {
            EnsureLayout();
            SetScrollOffset(_maxScroll);
        }

        /// <summary>Hit tests a point in client coordinates against the real tab silhouettes.</summary>
        public Chrome60HitTestInfo HitTest(Point point)
        {
            if (!ClientRectangle.Contains(point))
                return new Chrome60HitTestInfo(Chrome60HitArea.None, null, -1);

            EnsureLayout();

            // Z-order: the dragged tab, then the active tab, then left to right.
            // Tabs are checked before the new-tab button because the button sits behind:
            // its 5px tuck-under region belongs to the last tab.
            Chrome60TabLayout hit = null;
            for (int pass = 0; pass < 3 && hit == null; pass++)
            {
                foreach (var layout in _layouts)
                {
                    bool candidate = pass == 0 ? layout.IsDragging : pass == 1 ? layout.IsActive : true;
                    if (!candidate) continue;
                    if (layout.FillPath.IsVisible(point)) { hit = layout; break; }
                }
            }

            if (hit != null)
            {
                var area = Chrome60HitArea.Tab;
                if (hit.ShowCloseButton && hit.CloseButtonHitBounds.Contains(point)) area = Chrome60HitArea.TabCloseButton;
                else if (hit.ShowIcon && hit.IconBounds.Contains(point)) area = Chrome60HitArea.TabIcon;
                else if (hit.TextBounds.Contains(point)) area = Chrome60HitArea.TabText;

                return new Chrome60HitTestInfo(area, hit.Tab, hit.Index);
            }

            if (_showNewTabButton && _newTabButtonPath != null && _newTabButtonPath.IsVisible(point))
                return new Chrome60HitTestInfo(Chrome60HitArea.NewTabButton, null, -1);

            return new Chrome60HitTestInfo(Chrome60HitArea.TabStrip, null, -1);
        }

        public Chrome60Tab GetTabAt(Point point)
        {
            return HitTest(point).Tab;
        }

        /// <summary>Bounding box of a tab's silhouette in client (device) pixels.</summary>
        public Rectangle GetTabBounds(Chrome60Tab tab)
        {
            EnsureLayout();
            var layout = FindLayout(tab);
            return layout == null ? Rectangle.Empty : layout.Bounds;
        }

        /// <summary>A copy of the tab's closed silhouette path. The caller owns the returned path.</summary>
        public GraphicsPath GetTabPath(Chrome60Tab tab)
        {
            EnsureLayout();
            var layout = FindLayout(tab);
            return layout == null ? null : (GraphicsPath)layout.FillPath.Clone();
        }

        public Chrome60ButtonState GetCloseButtonState(Chrome60Tab tab)
        {
            if (ReferenceEquals(_pressedTab, tab) && _pressedArea == Chrome60HitArea.TabCloseButton && _dragTab == null)
                return _hoveredArea == Chrome60HitArea.TabCloseButton && ReferenceEquals(_hoveredTab, tab)
                    ? Chrome60ButtonState.Pressed
                    : Chrome60ButtonState.Normal;

            if (ReferenceEquals(_hoveredTab, tab) && _hoveredArea == Chrome60HitArea.TabCloseButton && _pressedTab == null && tab.Enabled)
                return Chrome60ButtonState.Hot;

            return Chrome60ButtonState.Normal;
        }

        public Chrome60ButtonState GetNewTabButtonState()
        {
            if (_newTabPressed) return _newTabHovered ? Chrome60ButtonState.Pressed : Chrome60ButtonState.Normal;
            return _newTabHovered && _pressedTab == null ? Chrome60ButtonState.Hot : Chrome60ButtonState.Normal;
        }

        #endregion

        #region Collection callbacks

        internal void OnTabInserted(int index, Chrome60Tab tab)
        {
            tab.Owner = this;
            tab.HoverProgress = 0f;
            AttachContent(tab);

            if (_selectedIndex >= index) _selectedIndex++;

            InvalidateLayout();
            OnTabAdded(new Chrome60TabEventArgs(tab, index));

            if (_selectedIndex < 0 && tab.Enabled)
                SelectTabCore(index, false);
            else
                UpdateContentVisibility();
        }

        internal void OnTabRemoving(int index, Chrome60Tab tab)
        {
            if (ReferenceEquals(_dragTab, tab)) EndDrag(false);
            if (ReferenceEquals(_pressedTab, tab)) { _pressedTab = null; _pressedArea = Chrome60HitArea.None; }
            if (ReferenceEquals(_hoveredTab, tab)) SetHovered(null, Chrome60HitArea.None);
            if (ReferenceEquals(_contextMenuTab, tab)) _contextMenuTab = null;
        }

        internal void OnTabRemoved(int index, Chrome60Tab tab)
        {
            DetachContent(tab, tab.Content);
            tab.Owner = null;

            int oldIndex = _selectedIndex;

            if (index == _selectedIndex)
            {
                // Chrome selects the tab to the right, or the new last tab when the last one closed.
                int next = Math.Min(index, _tabs.Count - 1);
                _selectedIndex = -1;
                InvalidateLayout();
                OnTabRemoved(new Chrome60TabEventArgs(tab, index));

                int pick = FindEnabledFrom(next, 1);
                if (pick < 0) pick = FindEnabledFrom(next, -1);
                if (pick >= 0)
                {
                    _selectedIndex = pick;
                    UpdateContentVisibility();
                    EnsureTabVisible(pick);
                    OnSelectedIndexChanged(new Chrome60SelectedTabChangedEventArgs(tab, oldIndex, _tabs[pick], pick));
                }
                else
                {
                    UpdateContentVisibility();
                    OnSelectedIndexChanged(new Chrome60SelectedTabChangedEventArgs(tab, oldIndex, null, -1));
                }
                return;
            }

            if (index < _selectedIndex) _selectedIndex--;
            InvalidateLayout();
            OnTabRemoved(new Chrome60TabEventArgs(tab, index));
        }

        internal void OnTabsClearing(List<Chrome60Tab> tabs)
        {
            EndDrag(false);
            _pressedTab = null;
            _pressedArea = Chrome60HitArea.None;
            SetHovered(null, Chrome60HitArea.None);
            _contextMenuTab = null;
        }

        internal void OnTabsCleared(List<Chrome60Tab> tabs)
        {
            var oldSelected = _selectedIndex >= 0 && _selectedIndex < tabs.Count ? tabs[_selectedIndex] : null;
            int oldIndex = _selectedIndex;
            _selectedIndex = -1;

            for (int i = 0; i < tabs.Count; i++)
            {
                DetachContent(tabs[i], tabs[i].Content);
                tabs[i].Owner = null;
            }

            InvalidateLayout();

            for (int i = 0; i < tabs.Count; i++)
                OnTabRemoved(new Chrome60TabEventArgs(tabs[i], i));

            if (oldIndex >= 0)
                OnSelectedIndexChanged(new Chrome60SelectedTabChangedEventArgs(oldSelected, oldIndex, null, -1));
        }

        internal void OnTabPropertyChanged(Chrome60Tab tab, bool affectsLayout)
        {
            if (affectsLayout) InvalidateLayout();
            else Invalidate();
        }

        internal void OnTabContentChanged(Chrome60Tab tab, Control oldContent)
        {
            DetachContent(tab, oldContent);
            AttachContent(tab);
            UpdateContentVisibility();
        }

        private Chrome60Tab SelectedTabUnchecked(int removedIndex)
        {
            return null;
        }

        #endregion

        #region Content hosting

        private void AttachContent(Chrome60Tab tab)
        {
            var host = _contentHost;
            var content = tab.Content;
            if (host == null || content == null) return;

            if (!ReferenceEquals(content.Parent, host))
            {
                content.Visible = false;
                content.Dock = DockStyle.Fill;
                host.Controls.Add(content);
            }
        }

        private void DetachContent(Chrome60Tab tab, Control content)
        {
            var host = _contentHost;
            if (host == null || content == null) return;
            if (ReferenceEquals(content.Parent, host))
                host.Controls.Remove(content);
        }

        private void UpdateContentVisibility()
        {
            var host = _contentHost;
            if (host == null) return;

            var selected = SelectedTab;
            host.SuspendLayout();
            try
            {
                // Show first so the host never flashes empty.
                if (selected != null && selected.Content != null && ReferenceEquals(selected.Content.Parent, host))
                {
                    selected.Content.Visible = true;
                    selected.Content.BringToFront();
                }

                foreach (var tab in _tabs)
                {
                    var content = tab.Content;
                    if (content == null || ReferenceEquals(tab, selected)) continue;
                    if (ReferenceEquals(content.Parent, host)) content.Visible = false;
                }
            }
            finally
            {
                host.ResumeLayout();
            }
        }

        #endregion

        #region Selection

        private void SelectTabCore(int index, bool raiseSelecting)
        {
            if (index < -1 || index >= _tabs.Count)
                throw new ArgumentOutOfRangeException("index");
            if (index == _selectedIndex) return;

            var oldTab = SelectedTab;
            int oldIndex = _selectedIndex;
            var newTab = index >= 0 ? _tabs[index] : null;

            if (raiseSelecting && newTab != null)
            {
                var cancel = new Chrome60TabCancelEventArgs(newTab, index);
                OnSelecting(cancel);
                if (cancel.Cancel) return;
            }

            _selectedIndex = index;
            InvalidateLayout();
            UpdateContentVisibility();
            if (index >= 0)
                EnsureTabVisible(index);
            OnSelectedIndexChanged(new Chrome60SelectedTabChangedEventArgs(oldTab, oldIndex, newTab, index));

            if (IsHandleCreated)
                AccessibilityNotifyClients(AccessibleEvents.Selection, index);
        }

        private void SelectAdjacent(int direction, bool wrap)
        {
            if (_tabs.Count == 0) return;
            int start = _selectedIndex < 0 ? (direction > 0 ? -1 : _tabs.Count) : _selectedIndex;

            for (int step = 1; step <= _tabs.Count; step++)
            {
                int i = start + direction * step;
                if (i < 0 || i >= _tabs.Count)
                {
                    if (!wrap) return;
                    i = ((i % _tabs.Count) + _tabs.Count) % _tabs.Count;
                }
                if (_tabs[i].Enabled)
                {
                    SelectTabCore(i, true);
                    return;
                }
            }
        }

        private int FindEnabledFrom(int index, int direction)
        {
            for (int i = index; i >= 0 && i < _tabs.Count; i += direction)
                if (_tabs[i].Enabled) return i;
            return -1;
        }

        #endregion

        #region Layout

        private void InvalidateLayout()
        {
            _layoutValid = false;
            Invalidate();
        }

        private void EnsureLayout()
        {
            if (!_layoutValid) PerformTabLayout();
        }

        private void SetScrollOffset(int value)
        {
            EnsureLayout();
            int clamped = Math.Max(0, Math.Min(_maxScroll, value));
            if (clamped == _scrollOffset) return;
            _scrollOffset = clamped;
            InvalidateLayout();
        }

        private void ClearLayouts()
        {
            foreach (var layout in _layouts) layout.Dispose();
            _layouts.Clear();
            if (_newTabButtonPath != null) { _newTabButtonPath.Dispose(); _newTabButtonPath = null; }
        }

        /// <summary>
        /// Chrome's TabStrip::GenerateIdealBounds: tabs share the available width, shrinking from the
        /// standard width toward the minimums, the active tab never going below MinimumActiveTabWidth.
        /// </summary>
        private void PerformTabLayout()
        {
            ClearLayouts();
            _layoutValid = true;

            float s = Scale;
            int count = _tabs.Count;

            int tabHeight = Round(Chrome60Metrics.TabHeight * s);
            int tabTop = ClientSize.Height - tabHeight;
            int overlap = Round(Chrome60Metrics.TabOverlap * s);
            int newTabWidth = Round(Chrome60Metrics.NewTabButtonWidth * s);
            int newTabHeight = Round(Chrome60Metrics.NewTabButtonHeight * s);
            int newTabOverlap = Round(Chrome60Metrics.NewTabButtonOverlap * s);
            int leftPad = Round(_leftPadding * s);
            int rightPad = Round(_rightPadding * s);

            int stripRight = ClientSize.Width - rightPad;
            int reservedForNewTab = _showNewTabButton ? newTabWidth - newTabOverlap : 0;
            int available = Math.Max(0, stripRight - leftPad - reservedForNewTab);

            // Fixed position for the pinned button (scroll mode): always at the right edge.
            int buttonFixedX = stripRight - newTabWidth;
            int buttonFixedY = tabTop + (tabHeight - newTabHeight) / 2;
            int viewportRight = buttonFixedX + newTabOverlap;

            double minInactive = Math.Max(2 * Chrome60Metrics.TabEndcapWidth + 4, _minimumInactiveTabWidth) * s;
            double minActive = Math.Max(_minimumInactiveTabWidth, _minimumActiveTabWidth) * s;
            double standard = Math.Max(_minimumActiveTabWidth, _standardTabWidth) * s;

            double inactiveWidth;
            double activeWidth;

            if (_sizingMode == Chrome60TabSizingMode.Fixed || count == 0)
            {
                inactiveWidth = activeWidth = _fixedTabWidth * s;
            }
            else
            {
                double totalOverlap = overlap * (double)(count - 1);
                double desired = (available + totalOverlap) / count;
                desired = Clamp(desired, minInactive, standard);
                inactiveWidth = activeWidth = desired;

                if (desired < minActive && _selectedIndex >= 0)
                {
                    activeWidth = minActive;
                    if (count > 1)
                        inactiveWidth = Clamp((available + totalOverlap - activeWidth) / (count - 1), minInactive, standard);
                }
            }

            // Total strip width at these sizes. When it still overflows, switch to
            // scroll mode: keep the small (minimum) sizes and scroll instead of
            // shrinking further. The add button pins to the right edge.
            double totalWidth = 0;
            for (int i = 0; i < count; i++)
                totalWidth += (i == _selectedIndex ? activeWidth : inactiveWidth);
            if (count > 1) totalWidth -= overlap * (double)(count - 1);

            bool overflow = _allowTabScroll && count > 0 && totalWidth > available;
            if (!overflow)
            {
                _scrollOffset = 0;
                _maxScroll = 0;
                _isScrolling = false;
                _tabViewport = ClientRectangle;
            }
            else
            {
                _maxScroll = Math.Max(0, (int)Math.Round(totalWidth - available));
                if (_scrollOffset < 0) _scrollOffset = 0;
                if (_scrollOffset > _maxScroll) _scrollOffset = _maxScroll;
                _isScrolling = true;
                _tabViewport = new Rectangle(0, 0, Math.Max(0, viewportRight), Math.Max(0, ClientSize.Height));
            }

            double x = leftPad - _scrollOffset;
            int lastRight = (int)Math.Round(x);

            for (int i = 0; i < count; i++)
            {
                var tab = _tabs[i];
                bool active = i == _selectedIndex;
                double width = active ? activeWidth : inactiveWidth;

                int left = (int)Math.Round(x);
                int right = (int)Math.Round(x + width);
                var bounds = new Rectangle(left, tabTop, right - left, tabHeight);

                bool dragging = ReferenceEquals(_dragTab, tab);
                if (dragging)
                    bounds.X = Math.Max(leftPad, Math.Min(_dragX, stripRight - bounds.Width));

                var layout = BuildTabLayout(tab, i, bounds, active, dragging, s);
                _layouts.Add(layout);

                lastRight = right;
                x += width - overlap;
            }

            if (_showNewTabButton)
            {
                int ntbY = buttonFixedY;
                int ntbX;
                if (_isScrolling)
                {
                    // Pinned: always visible at the right, never scrolls away.
                    ntbX = buttonFixedX;
                }
                else
                {
                    ntbX = count == 0 ? leftPad : lastRight - newTabOverlap;
                    ntbX = Math.Min(ntbX, stripRight - newTabWidth);
                    ntbX = Math.Max(ntbX, leftPad);
                }
                // Vertically centered within the tab row (not bottom-aligned):
                // a 16px button in a 29px tab row gets ~6-7px above and below.
                _newTabButtonBounds = new Rectangle(ntbX, ntbY, newTabWidth, newTabHeight);
                _newTabButtonPath = Chrome60Geometry.CreateNewTabButtonPath(_newTabButtonBounds, s);
            }
            else
            {
                _newTabButtonBounds = Rectangle.Empty;
            }
        }

        private Chrome60TabLayout BuildTabLayout(Chrome60Tab tab, int index, Rectangle bounds, bool active, bool dragging, float s)
        {
            var layout = new Chrome60TabLayout
            {
                Tab = tab,
                Index = index,
                Bounds = bounds,
                IsActive = active,
                IsDragging = dragging,
                FillPath = Chrome60Geometry.CreateTabPath(bounds, s, true),
                StrokePath = Chrome60Geometry.CreateTabPath(Chrome60Geometry.StrokeBoundsForTab(bounds), s, false)
            };

            int insetLeft = Round(Chrome60Metrics.TabContentLeftInset * s);
            int insetRight = Round(Chrome60Metrics.TabContentRightInset * s);
            int iconSize = Round(Chrome60Metrics.FaviconSize * s);
            int closeSize = Round(Chrome60Metrics.CloseButtonSize * s);
            int iconSpacing = Round(Chrome60Metrics.FaviconTitleSpacing * s);
            int closeSpacing = Round(Chrome60Metrics.TitleCloseSpacing * s);
            int closePad = Round(Chrome60Metrics.CloseButtonHitPadding * s);

            int contentLeft = bounds.X + insetLeft;
            int contentRight = bounds.Right - insetRight;
            int contentWidth = Math.Max(0, contentRight - contentLeft);

            // Chrome: the active tab always keeps its close box (dropping the favicon first);
            // inactive tabs only show one once they are wide enough.
            bool wantClose = _showCloseButtons && tab.CloseButtonVisible;
            bool showClose = wantClose && (active
                ? contentWidth >= closeSize
                : bounds.Width >= Round(Chrome60Metrics.MinimumInactiveWidthForCloseButton * s));

            int needForIcon = iconSize + (showClose ? iconSpacing + closeSize : 0);
            bool showIcon = _showIcons && contentWidth >= needForIcon;

            int centerY = bounds.Y + (bounds.Height - iconSize) / 2;
            layout.ShowIcon = showIcon;
            layout.ShowCloseButton = showClose;
            layout.IconBounds = new Rectangle(contentLeft, centerY, iconSize, iconSize);

            var closeBounds = new Rectangle(contentRight - closeSize, bounds.Y + (bounds.Height - closeSize) / 2, closeSize, closeSize);
            layout.CloseButtonBounds = closeBounds;
            var hit = closeBounds;
            hit.Inflate(closePad, closePad);
            layout.CloseButtonHitBounds = hit;

            int textLeft = showIcon ? layout.IconBounds.Right + iconSpacing : contentLeft;
            int textRight = showClose ? closeBounds.Left - closeSpacing : contentRight;
            layout.TextBounds = new Rectangle(textLeft, bounds.Y, Math.Max(0, textRight - textLeft), bounds.Height);

            return layout;
        }

        private Chrome60TabLayout FindLayout(Chrome60Tab tab)
        {
            foreach (var layout in _layouts)
                if (ReferenceEquals(layout.Tab, tab)) return layout;
            return null;
        }

        private void ApplyAutoHeight()
        {
            if (_autoHeight && Height != PreferredHeight)
                Height = PreferredHeight;
        }

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            if (_autoHeight) height = PreferredHeight;
            base.SetBoundsCore(x, y, width, height, specified);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            return new Size(Math.Max(proposedSize.Width, 0), PreferredHeight);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            InvalidateLayout();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            InvalidateLayout();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ApplyAutoHeight();
            InvalidateLayout();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyAutoHeight();
            InvalidateLayout();
        }

        #endregion

        #region Painting

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Everything is painted in OnPaint on the double buffer; skipping the background
            // erase is what prevents flicker during hover, drag and resize.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            EnsureLayout();

            var g = e.Graphics;
            float s = Scale;
            var strip = ClientRectangle;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.CompositingQuality = CompositingQuality.HighQuality;

            _renderer.DrawBackground(g, this, strip);

            Chrome60TabLayout active = null;
            Chrome60TabLayout dragged = null;

            // 1. New tab button first (behind): in fit mode the last tab tucks 5px over
            // its left edge; in scroll mode it is pinned at the right edge and tabs
            // scroll underneath its overlap. It is a skewed parallelogram, NOT a mini-tab.
            if (_showNewTabButton && _newTabButtonPath != null)
                _renderer.DrawNewTabButton(g, this, _newTabButtonBounds, _newTabButtonPath, GetNewTabButtonState(), _newTabHoverProgress, s);

            // Tabs paint clipped to the viewport so scrolled-out tabs never draw over
            // the pinned button (except the 5px tuck-under) or past the control edge.
            // The dragged tab floats above everything and stays unclipped.
            var clipState = g.Save();
            try
            {
                if (_isScrolling && !_tabViewport.IsEmpty)
                    g.SetClip(_tabViewport, System.Drawing.Drawing2D.CombineMode.Intersect);

                // 2+3. Inactive tabs back-to-front (right to left, so the left neighbour sits
                // on top). Fill AND stroke are drawn together per tab so the tab on top covers
                // the neighbour's slope in the 16px overlap: each gap shows ONE clean edge,
                // not a harsh "X" of two crossing outlines. Because fills are slightly
                // translucent (~90%), the covered edge still ghosts through very faintly,
                // which is the accurate Chrome 60 look.
                for (int i = _layouts.Count - 1; i >= 0; i--)
                {
                    var layout = _layouts[i];
                    if (layout.IsActive) { active = layout; continue; }
                    if (layout.IsDragging) { dragged = layout; continue; }
                    _renderer.DrawInactiveTabFill(g, this, layout);
                    _renderer.DrawTabStroke(g, this, layout);
                }

                // 4. Contents of inactive tabs.
                foreach (var layout in _layouts)
                {
                    if (layout.IsActive || layout.IsDragging) continue;
                    _renderer.DrawTabContent(g, this, layout, s);
                }

                // 5. Toolbar top line (clipped part), then the active tab covers it.
                _renderer.DrawToolbarLine(g, this, strip);

                if (active != null && !active.IsDragging)
                    PaintTopTab(g, active, s);
            }
            finally
            {
                g.Restore(clipState);
            }

            // In scroll mode the clipped line above stops at the viewport edge; finish
            // the tail under the pinned button so the separator still spans the strip.
            if (_isScrolling && _showNewTabButton && _tabViewport.Right < strip.Right)
            {
                var tail = Rectangle.FromLTRB(_tabViewport.Right, strip.Top, strip.Right, strip.Bottom);
                if (tail.Width > 0)
                    _renderer.DrawToolbarLine(g, this, tail);
            }

            // 6. Dragged tab floats above everything.
            if (dragged != null)
                PaintTopTab(g, dragged, s);
            else if (active != null && active.IsDragging)
                PaintTopTab(g, active, s);

            if (Focused && ShowFocusCues && active != null)
                _renderer.DrawFocusCue(g, this, active);

            base.OnPaint(e);
        }

        private void PaintTopTab(Graphics g, Chrome60TabLayout layout, float s)
        {
            if (layout.IsActive) _renderer.DrawActiveTabFill(g, this, layout);
            else _renderer.DrawInactiveTabFill(g, this, layout);
            _renderer.DrawTabStroke(g, this, layout);
            _renderer.DrawTabContent(g, this, layout, s);
        }

        #endregion

        #region Mouse

        /// <summary>Scrolls the strip with the mouse wheel (one notch ~= 3 small tabs). Only when overflowing.</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_allowTabScroll)
            {
                EnsureLayout();
                if (_isScrolling && e.Delta != 0)
                {
                    int step = Math.Max(20, Round(60f * Scale));
                    double notches = e.Delta / 120.0;
                    SetScrollOffset(_scrollOffset - (int)Math.Round(notches * step));
                    var handled = e as HandledMouseEventArgs;
                    if (handled != null) handled.Handled = true;
                    return;
                }
            }

            base.OnMouseWheel(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_dragTab != null)
            {
                UpdateDrag(e.Location);
                return;
            }

            if (_pressedTab != null && _pressButton == MouseButtons.Left && _pressedArea != Chrome60HitArea.TabCloseButton &&
                _allowTabReorder && _pressedTab.Enabled && _tabs.Count > 1 &&
                Math.Abs(e.X - _pressLocation.X) >= SystemInformation.DragSize.Width)
            {
                BeginDrag(_pressedTab, e.Location);
                return;
            }

            var hit = HitTest(e.Location);
            SetHovered(hit.IsOnTab ? hit.Tab : null, hit.Area);
            SetNewTabHovered(hit.Area == Chrome60HitArea.NewTabButton);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_dragTab == null)
            {
                SetHovered(null, Chrome60HitArea.None);
                SetNewTabHovered(false);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            var hit = HitTest(e.Location);
            _pressLocation = e.Location;
            _pressButton = e.Button;
            _pressedTab = null;
            _pressedArea = Chrome60HitArea.None;

            if (hit.Area == Chrome60HitArea.NewTabButton)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _newTabPressed = true;
                    Invalidate();
                }
                return;
            }

            if (!hit.IsOnTab) return;

            _pressedTab = hit.Tab;
            _pressedArea = hit.Area;

            if (e.Button == MouseButtons.Left && hit.Area != Chrome60HitArea.TabCloseButton && hit.Tab.Enabled)
                SelectedTab = hit.Tab;   // Chrome activates on mouse down

            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_dragTab != null)
            {
                EndDrag(true);
                _pressedTab = null;
                _pressedArea = Chrome60HitArea.None;
                OnMouseMove(e);
                return;
            }

            var hit = HitTest(e.Location);

            if (_newTabPressed)
            {
                _newTabPressed = false;
                Invalidate();
                if (hit.Area == Chrome60HitArea.NewTabButton && e.Button == MouseButtons.Left)
                    OnNewTabRequested(EventArgs.Empty);
            }

            var pressedTab = _pressedTab;
            var pressedArea = _pressedArea;
            _pressedTab = null;
            _pressedArea = Chrome60HitArea.None;

            if (pressedTab != null && ReferenceEquals(hit.Tab, pressedTab))
            {
                if (e.Button == MouseButtons.Left && pressedArea == Chrome60HitArea.TabCloseButton &&
                    hit.Area == Chrome60HitArea.TabCloseButton && pressedTab.Enabled)
                {
                    CloseTab(pressedTab);
                }
                else if (e.Button == MouseButtons.Middle && _closeOnMiddleClick && pressedTab.Enabled)
                {
                    CloseTab(pressedTab);
                }
            }

            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            var hit = HitTest(e.Location);
            if (hit.IsOnTab)
                OnTabMouseClick(new Chrome60TabMouseEventArgs(e, hit));
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            var hit = HitTest(e.Location);

            if (hit.IsOnTab)
            {
                OnTabMouseDoubleClick(new Chrome60TabMouseEventArgs(e, hit));
            }
            else if (hit.Area == Chrome60HitArea.TabStrip && e.Button == MouseButtons.Left && _newTabOnDoubleClick)
            {
                OnNewTabRequested(EventArgs.Empty);
            }
        }

        protected override void WndProc(ref Message m)
        {
            // Horizontal wheel (touchpads, Shift+wheel): scroll the strip when overflowing.
            const int WM_MOUSEHWHEEL = 0x020E;
            if (m.Msg == WM_MOUSEHWHEEL && _allowTabScroll)
            {
                EnsureLayout();
                if (_isScrolling)
                {
                    long wp = m.WParam.ToInt64();
                    short raw = unchecked((short)((wp >> 16) & 0xFFFF));
                    if (raw != 0)
                    {
                        int step = Math.Max(20, Round(60f * Scale));
                        double notches = raw / 120.0;
                        SetScrollOffset(_scrollOffset + (int)Math.Round(notches * step));
                    }
                    m.Result = (IntPtr)1;
                    return;
                }
            }

            if (m.Msg == WM_CONTEXTMENU && _tabContextMenuStrip != null)
            {
                int lp = unchecked((int)(long)m.LParam);
                Point screen = lp == -1
                    ? PointToScreen(GetKeyboardContextMenuAnchor())
                    : new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));

                var client = PointToClient(screen);
                var hit = HitTest(client);
                if (hit.IsOnTab || lp == -1 && SelectedTab != null)
                {
                    _contextMenuTab = hit.Tab ?? SelectedTab;
                    _tabContextMenuStrip.Show(screen);
                    return;
                }
            }

            base.WndProc(ref m);
        }

        private Point GetKeyboardContextMenuAnchor()
        {
            var selected = SelectedTab;
            if (selected == null) return new Point(0, Height);
            var bounds = GetTabBounds(selected);
            return new Point(bounds.X + Round(Chrome60Metrics.TabEndcapWidth * Scale), bounds.Bottom);
        }

        private void SetHovered(Chrome60Tab tab, Chrome60HitArea area)
        {
            bool tabChanged = !ReferenceEquals(_hoveredTab, tab);
            bool areaChanged = _hoveredArea != area;
            if (!tabChanged && !areaChanged) return;

            _hoveredTab = tab;
            _hoveredArea = area;

            if (tabChanged)
            {
                StartAnimation();
                OnHoveredTabChanged(EventArgs.Empty);
                SetToolTip(tab == null || !_showToolTips ? null : (tab.ToolTipText ?? tab.Text));
            }

            Invalidate();
        }

        private void SetNewTabHovered(bool hovered)
        {
            if (_newTabHovered == hovered) return;
            _newTabHovered = hovered;
            StartAnimation();
            Invalidate();
        }

        private void SetToolTip(string text)
        {
            if (string.Equals(_currentToolTip, text, StringComparison.Ordinal)) return;
            _currentToolTip = text;
            _toolTip.SetToolTip(this, text);
        }

        #endregion

        #region Drag reorder / detach

        private void BeginDrag(Chrome60Tab tab, Point location)
        {
            var bounds = GetTabBounds(tab);
            _dragTab = tab;
            _dragOffsetX = location.X - bounds.X;
            _dragX = bounds.X;
            Capture = true;
            SetToolTip(null);
            InvalidateLayout();
        }

        private void UpdateDrag(Point location)
        {
            if (_dragTab == null) return;

            if (_allowTabDetach && Math.Abs(location.Y - _pressLocation.Y) >= Round(Chrome60Metrics.DetachThreshold * Scale))
            {
                var tab = _dragTab;
                int index = _tabs.IndexOf(tab);
                var args = new Chrome60TabDetachEventArgs(tab, index, PointToScreen(location));
                OnTabDetachRequested(args);
                if (!args.Cancel)
                {
                    EndDrag(false);
                    _pressedTab = null;
                    _pressedArea = Chrome60HitArea.None;
                    if (_tabs.Contains(tab)) _tabs.Remove(tab);
                    return;
                }
            }

            _dragX = location.X - _dragOffsetX;

            // Auto-scroll while dragging near the viewport edges when overflowing.
            if (_allowTabScroll)
            {
                EnsureLayout();
                if (_isScrolling && !_tabViewport.IsEmpty)
                {
                    int edge = Math.Max(16, Round(20f * Scale));
                    int step = Math.Max(8, Round(12f * Scale));
                    if (location.X < _tabViewport.Left + edge && _scrollOffset > 0)
                        SetScrollOffset(_scrollOffset - step);
                    else if (location.X > _tabViewport.Right - edge && _scrollOffset < _maxScroll)
                        SetScrollOffset(_scrollOffset + step);
                }
            }

            _layoutValid = false;
            EnsureLayout();

            // Reorder when the dragged tab's centre passes a neighbour's centre.
            var dragLayout = FindLayout(_dragTab);
            if (dragLayout == null) return;

            int dragCenter = dragLayout.Bounds.X + dragLayout.Bounds.Width / 2;
            int current = _tabs.IndexOf(_dragTab);
            int target = current;

            for (int i = 0; i < _layouts.Count; i++)
            {
                if (i == current) continue;
                var other = _layouts[i];
                int otherCenter = other.Bounds.X + other.Bounds.Width / 2;
                if (i < current && dragCenter < otherCenter) { target = i; break; }
                if (i > current && dragCenter > otherCenter) target = i;
            }

            if (target != current)
                MoveTab(current, target);

            Invalidate();
        }

        private void EndDrag(bool commit)
        {
            if (_dragTab == null) return;
            _dragTab = null;
            Capture = false;
            InvalidateLayout();
        }

        #endregion

        #region Keyboard

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Home:
                case Keys.End:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;

            switch (e.KeyCode)
            {
                case Keys.Left:
                    SelectAdjacent(-1, true);
                    e.Handled = true;
                    break;
                case Keys.Right:
                    SelectAdjacent(1, true);
                    e.Handled = true;
                    break;
                case Keys.Home:
                    if (_tabs.Count > 0) { int i = FindEnabledFrom(0, 1); if (i >= 0) SelectTabCore(i, true); }
                    e.Handled = true;
                    break;
                case Keys.End:
                    if (_tabs.Count > 0) { int i = FindEnabledFrom(_tabs.Count - 1, -1); if (i >= 0) SelectTabCore(i, true); }
                    e.Handled = true;
                    break;
                case Keys.Space:
                case Keys.Enter:
                    e.Handled = true;
                    break;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Focused)
            {
                switch (keyData)
                {
                    case Keys.Control | Keys.Tab:
                    case Keys.Control | Keys.PageDown:
                        SelectAdjacent(1, true);
                        return true;
                    case Keys.Control | Keys.Shift | Keys.Tab:
                    case Keys.Control | Keys.PageUp:
                        SelectAdjacent(-1, true);
                        return true;
                    case Keys.Control | Keys.W:
                    case Keys.Control | Keys.F4:
                        if (SelectedTab != null && SelectedTab.Enabled) CloseTab(SelectedTab);
                        return true;
                    case Keys.Control | Keys.T:
                        OnNewTabRequested(EventArgs.Empty);
                        return true;
                }

                // Ctrl+1..8 select that tab, Ctrl+9 selects the last (Chrome).
                if ((keyData & Keys.Modifiers) == Keys.Control)
                {
                    var key = keyData & Keys.KeyCode;
                    if (key >= Keys.D1 && key <= Keys.D8)
                    {
                        int i = key - Keys.D1;
                        if (i < _tabs.Count && _tabs[i].Enabled) SelectTabCore(i, true);
                        return true;
                    }
                    if (key == Keys.D9 && _tabs.Count > 0)
                    {
                        int i = FindEnabledFrom(_tabs.Count - 1, -1);
                        if (i >= 0) SelectTabCore(i, true);
                        return true;
                    }
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        #endregion

        #region Animation

        private void StartAnimation()
        {
            if (_hoverAnimationDuration <= 0)
            {
                SnapAnimation();
                return;
            }

            if (!_animationTimer.Enabled)
            {
                _lastAnimationTick = DateTime.UtcNow;
                _animationTimer.Start();
            }
        }

        private void SnapAnimation()
        {
            foreach (var tab in _tabs)
                tab.HoverProgress = HoverTarget(tab);
            _newTabHoverProgress = _newTabHovered ? 1f : 0f;
            Invalidate();
        }

        private float HoverTarget(Chrome60Tab tab)
        {
            return ReferenceEquals(tab, _hoveredTab) && !ReferenceEquals(tab, SelectedTab) && tab.Enabled && _dragTab == null ? 1f : 0f;
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            float dt = (float)(now - _lastAnimationTick).TotalMilliseconds;
            _lastAnimationTick = now;

            float step = _hoverAnimationDuration <= 0 ? 1f : dt / _hoverAnimationDuration;
            bool busy = false;

            foreach (var tab in _tabs)
                busy |= Approach(ref tab.HoverProgress, HoverTarget(tab), step);

            busy |= Approach(ref _newTabHoverProgress, _newTabHovered ? 1f : 0f, step);

            if (!busy) _animationTimer.Stop();
            Invalidate();
        }

        private static bool Approach(ref float value, float target, float step)
        {
            if (value == target) return false;
            if (value < target) value = Math.Min(target, value + step);
            else value = Math.Max(target, value - step);
            return value != target;
        }

        #endregion

        #region Event raisers

        protected virtual void OnNewTabRequested(EventArgs e)
        {
            var handler = NewTabRequested;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnSelecting(Chrome60TabCancelEventArgs e)
        {
            var handler = Selecting;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnSelectedIndexChanged(Chrome60SelectedTabChangedEventArgs e)
        {
            var handler = SelectedIndexChanged;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnTabClosing(Chrome60TabCancelEventArgs e)
        {
            var handler = TabClosing;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnTabClosed(Chrome60TabEventArgs e)
        {
            var handler = TabClosed;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnTabAdded(Chrome60TabEventArgs e)
        {
            var handler = TabAdded;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnTabRemoved(Chrome60TabEventArgs e)
        {
            var handler = TabRemoved;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnTabMoved(Chrome60TabMovedEventArgs e)
        {
            var handler = TabMoved;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnTabDetachRequested(Chrome60TabDetachEventArgs e)
        {
            var handler = TabDetachRequested;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnTabMouseClick(Chrome60TabMouseEventArgs e)
        {
            var handler = TabMouseClick;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnTabMouseDoubleClick(Chrome60TabMouseEventArgs e)
        {
            var handler = TabMouseDoubleClick;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnHoveredTabChanged(EventArgs e)
        {
            var handler = HoveredTabChanged;
            if (handler != null) handler(this, e);
        }

        #endregion

        #region Accessibility

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new Chrome60TabControlAccessibleObject(this);
        }

        internal AccessibleObject CreateTabAccessibleObject(int index)
        {
            return new Chrome60TabAccessibleObject(this, index);
        }

        private sealed class Chrome60TabControlAccessibleObject : ControlAccessibleObject
        {
            private readonly Chrome60TabControl _owner;

            public Chrome60TabControlAccessibleObject(Chrome60TabControl owner)
                : base(owner)
            {
                _owner = owner;
            }

            public override AccessibleRole Role
            {
                get { return AccessibleRole.PageTabList; }
            }

            public override int GetChildCount()
            {
                return _owner._tabs.Count + (_owner._showNewTabButton ? 1 : 0);
            }

            public override AccessibleObject GetChild(int index)
            {
                if (index >= 0 && index < _owner._tabs.Count)
                    return _owner.CreateTabAccessibleObject(index);
                if (_owner._showNewTabButton && index == _owner._tabs.Count)
                    return new Chrome60NewTabButtonAccessibleObject(_owner);
                return null;
            }

            public override AccessibleObject GetSelected()
            {
                return _owner._selectedIndex >= 0 ? _owner.CreateTabAccessibleObject(_owner._selectedIndex) : null;
            }

            public override AccessibleObject GetFocused()
            {
                return _owner.Focused ? GetSelected() : null;
            }

            public override AccessibleObject HitTest(int x, int y)
            {
                var hit = _owner.HitTest(_owner.PointToClient(new Point(x, y)));
                if (hit.IsOnTab) return _owner.CreateTabAccessibleObject(hit.Index);
                if (hit.Area == Chrome60HitArea.NewTabButton) return new Chrome60NewTabButtonAccessibleObject(_owner);
                return hit.Area == Chrome60HitArea.None ? null : this;
            }
        }

        private sealed class Chrome60TabAccessibleObject : AccessibleObject
        {
            private readonly Chrome60TabControl _owner;
            private readonly int _index;

            public Chrome60TabAccessibleObject(Chrome60TabControl owner, int index)
            {
                _owner = owner;
                _index = index;
            }

            private Chrome60Tab Tab
            {
                get { return _index >= 0 && _index < _owner._tabs.Count ? _owner._tabs[_index] : null; }
            }

            public override string Name
            {
                get { var tab = Tab; return tab == null ? string.Empty : tab.Text; }
                set { var tab = Tab; if (tab != null) tab.Text = value; }
            }

            public override AccessibleRole Role
            {
                get { return AccessibleRole.PageTab; }
            }

            public override AccessibleObject Parent
            {
                get { return _owner.AccessibilityObject; }
            }

            public override Rectangle Bounds
            {
                get
                {
                    var tab = Tab;
                    if (tab == null) return Rectangle.Empty;
                    return _owner.RectangleToScreen(_owner.GetTabBounds(tab));
                }
            }

            public override AccessibleStates State
            {
                get
                {
                    var tab = Tab;
                    if tab == null) return AccessibleStates.Invisible;

                    var state = AccessibleStates.Selectable;
                    if (!tab.Enabled) state |= AccessibleStates.Unavailable;
                    else state |= AccessibleStates.Focusable;
                    if (_index == _owner._selectedIndex)
                    {
                        state |= AccessibleStates.Selected;
                        if (_owner.Focused) state |= AccessibleStates.Focused;
                    }
                    return state;
                }
            }

            public override string DefaultAction
            {
                get { return "Switch"; }
            }

            public override void DoDefaultAction()
            {
                var tab = Tab;
                if (tab != null && tab.Enabled) _owner.SelectTabCore(_index, true);
            }

            public override void Select(AccessibleSelection flags)
            {
                if ((flags & (AccessibleSelection.TakeSelection | AccessibleSelection.TakeFocus)) != 0)
                    DoDefaultAction();
            }

            public override AccessibleObject Navigate(AccessibleNavigation navdir)
            {
                switch (navdir)
                {
                    case AccessibleNavigation.Next:
                    case AccessibleNavigation.Right:
                        return _index + 1 < _owner._tabs.Count ? _owner.CreateTabAccessibleObject(_index + 1) : null;
                    case AccessibleNavigation.Previous:
                    case AccessibleNavigation.Left:
                        return _index > 0 ? _owner.CreateTabAccessibleObject(_index - 1) : null;
                }
                return base.Navigate(navdir);
            }
        }

        private sealed class Chrome60NewTabButtonAccessibleObject : AccessibleObject
        {
            private readonly Chrome60TabControl _owner;

            public Chrome60NewTabButtonAccessibleObject(Chrome60TabControl owner)
            {
                _owner = owner;
            }

            public override string Name { get { return "New Tab"; } set { } }
            public override AccessibleRole Role { get { return AccessibleRole.PushButton; } }
            public override AccessibleObject Parent { get { return _owner.AccessibilityObject; } }
            public override Rectangle Bounds { get { return _owner.RectangleToScreen(_owner.NewTabButtonBounds); } }
            public override string DefaultAction { get { return "Press"; } }
            public override AccessibleStates State { get { return AccessibleStates.Focusable; } }

            public override void DoDefaultAction()
            {
                _owner.OnNewTabRequested(EventArgs.Empty);
            }
        }

        #endregion

        #region Helpers

        private static Font CreateDefaultTabFont()
        {
            // Chrome 60 on Windows renders tab titles with the system UI font at 12px (9pt): Segoe UI.
            // GDI+ silently substitutes a default family if Segoe UI is missing, so this never throws.
            return new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        }

        private static int Round(float value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (max < min) max = min;
            return value < min ? min : (value > max ? max : value);
        }

        private void SetColor(ref Color field, Color value)
        {
            if (field == value) return;
            field = value;
            Invalidate();
        }

        private void SetInt(ref int field, int value)
        {
            if (field == value) return;
            field = value;
            InvalidateLayout();
        }

        private void SetBool(ref bool field, bool value, bool affectsLayout)
        {
            if (field == value) return;
            field = value;
            if (affectsLayout) InvalidateLayout();
            else Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer.Stop();
                _animationTimer.Tick -= OnAnimationTick;
                _animationTimer.Dispose();
                _toolTip.Dispose();
                ClearLayouts();
                if (_fontIsDefault && base.Font != null) base.Font.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
