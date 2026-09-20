using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Forms;

namespace Chrome60Tabs
{
    /// <summary>
    /// Ordered collection of tabs. Insertions, removals and replacements notify the owning control so
    /// selection, hosted content and layout stay consistent.
    /// </summary>
    public class Chrome60TabCollection : Collection<Chrome60Tab>
    {
        private readonly Chrome60TabControl _owner;

        internal bool SuppressOwnerNotifications;

        internal Chrome60TabCollection(Chrome60TabControl owner)
        {
            _owner = owner;
        }

        public Chrome60Tab Add(string text)
        {
            var tab = new Chrome60Tab(text);
            Add(tab);
            return tab;
        }

        public Chrome60Tab Add(string text, Image icon)
        {
            var tab = new Chrome60Tab(text, icon);
            Add(tab);
            return tab;
        }

        public Chrome60Tab Add(string text, Image icon, Control content)
        {
            var tab = new Chrome60Tab(text, icon, content);
            Add(tab);
            return tab;
        }

        public void AddRange(IEnumerable<Chrome60Tab> tabs)
        {
            if (tabs == null) throw new ArgumentNullException("tabs");
            foreach (var tab in tabs) Add(tab);
        }

        protected override void InsertItem(int index, Chrome60Tab item)
        {
            if (item == null) throw new ArgumentNullException("item");
            if (item.Owner != null && !SuppressOwnerNotifications)
                throw new InvalidOperationException("The tab already belongs to a Chrome60TabControl.");

            base.InsertItem(index, item);
            if (!SuppressOwnerNotifications) _owner.OnTabInserted(index, item);
        }

        protected override void RemoveItem(int index)
        {
            var item = this[index];
            if (!SuppressOwnerNotifications) _owner.OnTabRemoving(index, item);
            base.RemoveItem(index);
            if (!SuppressOwnerNotifications) _owner.OnTabRemoved(index, item);
        }

        protected override void SetItem(int index, Chrome60Tab item)
        {
            if (item == null) throw new ArgumentNullException("item");
            var old = this[index];
            if (ReferenceEquals(old, item)) return;

            _owner.OnTabRemoving(index, old);
            base.SetItem(index, item);
            _owner.OnTabRemoved(index, old);
            _owner.OnTabInserted(index, item);
        }

        protected override void ClearItems()
        {
            var snapshot = new List<Chrome60Tab>(this);
            _owner.OnTabsClearing(snapshot);
            base.ClearItems();
            _owner.OnTabsCleared(snapshot);
        }
    }
}
