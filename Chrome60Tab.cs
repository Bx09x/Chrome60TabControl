using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Chrome60Tabs
{
    /// <summary>
    /// One tab in a <see cref="Chrome60TabControl"/>. Plain data object; the control owns layout,
    /// rendering and interaction.
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class Chrome60Tab
    {
        private string _text = "New Tab";
        private Image _icon;
        private Control _content;
        private bool _enabled = true;
        private bool _closeButtonVisible = true;
        private string _toolTipText;
        private object _tag;

        public Chrome60Tab()
        {
        }

        public Chrome60Tab(string text)
        {
            _text = text ?? string.Empty;
        }

        public Chrome60Tab(string text, Image icon)
            : this(text)
        {
            _icon = icon;
        }

        public Chrome60Tab(string text, Image icon, Control content)
            : this(text, icon)
        {
            _content = content;
        }

        /// <summary>The control this tab currently belongs to, or null.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Chrome60TabControl Owner { get; internal set; }

        /// <summary>Title shown in the tab. Faded at the tail when it does not fit (Chrome FADE_TAIL eliding).</summary>
        [Category("Appearance")]
        [DefaultValue("New Tab")]
        [Localizable(true)]
        public string Text
        {
            get { return _text; }
            set
            {
                value = value ?? string.Empty;
                if (_text == value) return;
                _text = value;
                NotifyChanged(false);
            }
        }

        /// <summary>16x16 (logical) favicon. Transparent PNG/ICO images render correctly. Null draws the default page glyph.</summary>
        [Category("Appearance")]
        [DefaultValue(null)]
        public Image Icon
        {
            get { return _icon; }
            set
            {
                if (ReferenceEquals(_icon, value)) return;
                _icon = value;
                NotifyChanged(false);
            }
        }

        /// <summary>
        /// Optional page content. When the owning control has a <see cref="Chrome60TabControl.ContentHost"/>,
        /// the content is docked into it and shown/hidden with the tab.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Control Content
        {
            get { return _content; }
            set
            {
                if (ReferenceEquals(_content, value)) return;
                var old = _content;
                _content = value;
                var owner = Owner;
                if (owner != null) owner.OnTabContentChanged(this, old);
            }
        }

        /// <summary>Disabled tabs render greyed out and cannot be selected or dragged from the UI.</summary>
        [Category("Behavior")]
        [DefaultValue(true)]
        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                NotifyChanged(false);
            }
        }

        /// <summary>Whether this tab may show a close box (still subject to the control's <see cref="Chrome60TabControl.ShowCloseButtons"/>).</summary>
        [Category("Behavior")]
        [DefaultValue(true)]
        public bool CloseButtonVisible
        {
            get { return _closeButtonVisible; }
            set
            {
                if (_closeButtonVisible == value) return;
                _closeButtonVisible = value;
                NotifyChanged(true);
            }
        }

        /// <summary>Tooltip text. When null the tab title is used (Chrome shows the full title on hover).</summary>
        [Category("Appearance")]
        [DefaultValue(null)]
        [Localizable(true)]
        public string ToolTipText
        {
            get { return _toolTipText; }
            set { _toolTipText = value; }
        }

        /// <summary>Arbitrary user data.</summary>
        [Category("Data")]
        [DefaultValue(null)]
        [TypeConverter(typeof(StringConverter))]
        public object Tag
        {
            get { return _tag; }
            set { _tag = value; }
        }

        /// <summary>Index within the owner's <see cref="Chrome60TabControl.Tabs"/>, or -1.</summary>
        [Browsable(false)]
        public int Index
        {
            get
            {
                var owner = Owner;
                return owner == null ? -1 : owner.Tabs.IndexOf(this);
            }
        }

        /// <summary>True when this is the owner's selected tab.</summary>
        [Browsable(false)]
        public bool IsSelected
        {
            get
            {
                var owner = Owner;
                return owner != null && ReferenceEquals(owner.SelectedTab, this);
            }
        }

        /// <summary>Hover glow progress 0..1, animated by the owner.</summary>
        internal float HoverProgress;

        /// <summary>Selects this tab in its owner.</summary>
        public void Select()
        {
            var owner = Owner;
            if (owner != null) owner.SelectedTab = this;
        }

        /// <summary>Requests closing this tab (raises the owner's cancellable TabClosing event).</summary>
        public bool Close()
        {
            var owner = Owner;
            return owner != null && owner.CloseTab(this);
        }

        public override string ToString()
        {
            return _text;
        }

        private void NotifyChanged(bool affectsLayout)
        {
            var owner = Owner;
            if (owner != null) owner.OnTabPropertyChanged(this, affectsLayout);
        }
    }
}
