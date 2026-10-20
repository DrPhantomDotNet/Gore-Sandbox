using System;
using System.Collections.Generic;

namespace SolarEclipse
{
    /// <summary>Fixed-capacity ring pool: when full, the oldest item is recycled.</summary>
    public sealed class RingPool<T> where T : class
    {
        readonly List<T> items = new List<T>();
        readonly Func<T> create;
        int next;
        public int Capacity { get; set; }

        public RingPool(int capacity, Func<T> create)
        {
            Capacity = Math.Max(1, capacity);
            this.create = create;
        }

        public IReadOnlyList<T> Items => items;

        public T Next()
        {
            if (items.Count < Capacity)
            {
                var item = create();
                items.Add(item);
                return item;
            }
            if (next >= items.Count) next = 0;
            return items[next++];
        }
    }
}
