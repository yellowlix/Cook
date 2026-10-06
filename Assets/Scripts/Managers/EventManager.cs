using System;
using System.Collections.Generic;

namespace Cook.Managers
{
    /// <summary>主线程同步事件通知。以事件类型分组，不依赖 Unity 或具体玩法。</summary>
    public sealed class EventManager
    {
        private readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            handlers.TryGetValue(typeof(T), out Delegate current);
            handlers[typeof(T)] = Delegate.Combine(current, handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!handlers.TryGetValue(typeof(T), out Delegate current)) return;
            Delegate remaining = Delegate.Remove(current, handler);
            if (remaining == null) handlers.Remove(typeof(T));
            else handlers[typeof(T)] = remaining;
        }

        public void Publish<T>(T message)
        {
            if (!handlers.TryGetValue(typeof(T), out Delegate current)) return;
            // 委托不可变：回调中的订阅变更不修改本轮派发名单。
            ((Action<T>)current).Invoke(message);
        }

        public void Clear() => handlers.Clear();
    }
}
