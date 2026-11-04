using System;
using System.Collections.Generic;
using UnityEngine;

namespace Damdor.Temporio.Pooling
{
    public class TemporioPooling : IDisposable
    {
        private readonly ObjectPool<InternalTimer> internalTimerDataPool =
            new(100, data => data.Reset(), () => new InternalTimer());
        private readonly ObjectPool<TimerInvocationWithoutParameter> timerInvocationDataWithoutParameterPool =
            new(100, data => data.Reset(), () => new TimerInvocationWithoutParameter());
        private readonly Dictionary<Type, ObjectPool<TimerInvocationWithParameter>> timerInvocationDataWithParameterPools =
            new();
        private readonly ObjectPool<InternalTimerBuilder> timerBuilderPool =
            new(100, data => data.Reset(), () => new InternalTimerBuilder());
        private readonly ObjectPool<GameObjectTimerGuard> gameObjectTimerGuardPool =
            new(100, data => data.Reset(), () => new GameObjectTimerGuard());
        private readonly ObjectPool<ConditionTimerGuard> conditionTimerGuardPool =
            new(100, data => data.Reset(), () => new ConditionTimerGuard());
        private readonly Dictionary<Type, ObjectPool<ParametrizeConditionTimerGuard>> parametrizeConditionTimerGuardPools =
            new();

        internal InternalTimer PopInternalTimerData() => new InternalTimer();
        internal void ReleaseInternalTimerData(InternalTimer data) {}
        
        internal TimerInvocationWithoutParameter PopTimerInvocationDataWithoutParameter() => new();
        internal void ReleaseTimerInvocationDataWithoutParameter(TimerInvocationWithoutParameter invocation) {}
        
        internal TimerInvocationWithParameter<T> PopTimerInvocationDataWithParameter<T>() => new();
        internal void ReleaseTimerInvocationDataWithParameter(TimerInvocationWithParameter invocation) {}
        
        internal InternalTimerBuilder PopInternalTimerBuilder() => new InternalTimerBuilder();
        internal void ReleaseInternalTimerBuilder(InternalTimerBuilder timerBuilder) {}
        
        internal GameObjectTimerGuard PopGameObjectTimerGuard() => new GameObjectTimerGuard();
        internal void ReleaseGameObjectTimerGuard(GameObjectTimerGuard guard) {}
        
        internal ConditionTimerGuard PopConditionTimerGuard() => new ConditionTimerGuard();
        internal void ReleaseConditionTimerGuard(ConditionTimerGuard guard) {}
        
        internal ParametrizeConditionTimerGuard<T> PopParametrizeConditionTimerGuard<T>() => new();
        internal void ReleaseParametrizeConditionTimerGuard(ParametrizeConditionTimerGuard guard) {}
        
        private sealed class ObjectPool<T> where T : class
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
        }

        public void Dispose()
        {
        }
    }
}