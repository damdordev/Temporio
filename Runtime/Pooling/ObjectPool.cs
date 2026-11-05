using System;
using System.Collections.Generic;
using UnityEngine;

namespace Damdor.Temporio
{
    internal sealed class ObjectPool<T> : IDisposable
        where T : class
    {
        private readonly Stack<T> items = new();
        private readonly Action<T> reset;
        private readonly bool resetOnCreate;
        private int maxSize;
        private readonly Func<T> create;

        public ObjectPool(int maxSize, Action<T> reset, Func<T> create)
        {
            this.reset = reset;
            this.create = create;
            MaxSize = maxSize;
        }

        public int MaxSize
        {
            get => maxSize;
            set
            {
                maxSize = Mathf.Max(value, 0);
                while (items.Count > maxSize) items.Pop();
            }
        }

        public T Pop()
        {
            if (items.Count > 0) return items.Pop();
            var item = create();
            if (resetOnCreate) reset(item);
            return item;
        }
            
        public T Pop(Func<T> create)
        {
            if (items.Count > 0) return items.Pop();
            var item = create();
            if (resetOnCreate) reset(item);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            reset(item);
            if (items.Count < maxSize) items.Push(item);
        }

        public void Dispose()
        {
            items.Clear();
        }
    }
}