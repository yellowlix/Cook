using System;
using System.Collections.Generic;
using Cook.Core;

namespace Cook.Managers
{
    /// <summary>成品库存规则，不依赖 Unity 或 UI；调用方负责广播入库通知。</summary>
    public sealed class InventoryManager
    {
        public const int Capacity = 3;
        private readonly List<StoredDish> items = new List<StoredDish>();
        private readonly IReadOnlyList<StoredDish> view;

        public InventoryManager() { view = items.AsReadOnly(); }
        public IReadOnlyList<StoredDish> Items => view;
        public bool IsFull => items.Count >= Capacity;

        public bool TryAdd(ProductionResult result, out StoredDish stored)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            foreach (StoredDish item in items)
            {
                if (ReferenceEquals(item.Result, result)) { stored = item; return true; }
            }
            stored = null;
            if (IsFull) return false;
            stored = new StoredDish(result);
            items.Add(stored);
            return true;
        }

        public StoredDish Find(string id)
        {
            foreach (StoredDish item in items) if (item.Id == id) return item;
            return null;
        }
    }
}
