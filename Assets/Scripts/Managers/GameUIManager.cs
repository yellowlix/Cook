using System;
using System.Collections.Generic;
using Cook.UI;

namespace Cook.Managers
{
    /// <summary>只管理注册的场景面板；不加载资源、不持有 Canvas。</summary>
    public sealed class GameUIManager
    {
        private readonly Dictionary<Type, BasePanel> panels = new Dictionary<Type, BasePanel>();

        public void Register(BasePanel panel)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            panels[panel.GetType()] = panel;
        }

        public void Unregister(BasePanel panel)
        {
            if (ReferenceEquals(panel, null)) return;
            Type type = panel.GetType();
            if (panels.TryGetValue(type, out BasePanel registered) && ReferenceEquals(registered, panel))
                panels.Remove(type);
        }

        public T GetPanel<T>() where T : BasePanel
        {
            if (!panels.TryGetValue(typeof(T), out BasePanel panel)) return null;
            if (panel != null) return panel as T;
            panels.Remove(typeof(T));
            return null;
        }

        public T Show<T>() where T : BasePanel
        {
            T panel = GetPanel<T>();
            if (panel != null) panel.Show();
            return panel;
        }

        public void Hide<T>() where T : BasePanel
        {
            T panel = GetPanel<T>();
            if (panel != null) panel.Hide();
        }

        public bool IsShow<T>() where T : BasePanel
        {
            T panel = GetPanel<T>();
            return panel != null && panel.IsShow;
        }

        public void Clear() => panels.Clear();
    }
}
