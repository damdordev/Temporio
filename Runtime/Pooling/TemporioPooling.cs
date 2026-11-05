using System;
using System.Collections.Generic;

namespace Damdor.Temporio
{
    internal class TemporioPooling : IDisposable
    {
        private readonly ObjectPool<InternalTimer> internalTimerPool =
            new(500, data => data.Reset(), () => new InternalTimer());
        private readonly ObjectPool<TimerInvocationWithoutParameter> timerInvocationWithoutParameterPool =
            new(500, data => data.Reset(), () => new TimerInvocationWithoutParameter());
        private readonly ObjectPool<InternalTimerBuilder> timerBuilderPool =
            new(50, data => data.Reset(), () => new InternalTimerBuilder());
        private readonly ObjectPool<GameObjectTimerGuard> gameObjectTimerGuardPool =
            new(100, data => data.Reset(), () => new GameObjectTimerGuard());
        private readonly ObjectPool<ConditionTimerGuard> conditionTimerGuardPool =
            new(100, data => data.Reset(), () => new ConditionTimerGuard());
        private readonly Dictionary<Type, ObjectPool<TimerInvocation>>
            timerInvocationWithParameterPools = new();
        private readonly Dictionary<Type, ObjectPool<TimerGuard>>
            parametrizeConditionTimerGuardPools = new();

        internal InternalTimer PopInternalTimer() => internalTimerPool.Pop();
        internal void ReleaseInternalTimer(InternalTimer data) => internalTimerPool.Release(data);

        internal TimerInvocationWithoutParameter PopTimerInvocationWithoutParameter() => timerInvocationWithoutParameterPool.Pop();
        internal TimerInvocationWithParameter<T> PopTimerInvocationWithParameter<T>() => GetTimerInvocationWithParameterPool<T>().Pop() as TimerInvocationWithParameter<T>;
        internal void ReleaseTimerInvocationWithoutParameter(TimerInvocationWithoutParameter invocation) => timerInvocationWithoutParameterPool.Release(invocation);
        internal void ReleaseTimerInvocationWithParameter<T>(TimerInvocationWithParameter<T> invocation) => GetTimerInvocationWithParameterPool<T>().Release(invocation);
        
        private ObjectPool<TimerInvocation> GetTimerInvocationWithParameterPool<T>()
        {
            var type = typeof(T);
            if (timerInvocationWithParameterPools.TryGetValue(type, out var pool)) return pool;
            pool = new ObjectPool<TimerInvocation>(
                50,
                data => data.Reset(),
                () => new TimerInvocationWithParameter<T>()
            );
            timerInvocationWithParameterPools[type] = pool;
            return pool;
        }
        
        internal void ReleaseTimerInvocation(TimerInvocation invocation)
        {
            if (invocation is TimerInvocationWithoutParameter withoutParameter)
            {
                timerInvocationWithoutParameterPool.Release(withoutParameter);
            }
        }
        
        internal InternalTimerBuilder PopInternalTimerBuilder() => timerBuilderPool.Pop();
        internal void ReleaseInternalTimerBuilder(InternalTimerBuilder timerBuilder) => timerBuilderPool.Release(timerBuilder);

        internal GameObjectTimerGuard PopGameObjectTimerGuard() => gameObjectTimerGuardPool.Pop();
        internal void ReleaseGameObjectTimerGuard(GameObjectTimerGuard guard) => gameObjectTimerGuardPool.Release(guard);
        internal ConditionTimerGuard PopConditionTimerGuard() => conditionTimerGuardPool.Pop();
        internal void ReleaseConditionTimerGuard(ConditionTimerGuard guard) => conditionTimerGuardPool.Release(guard);

        internal ParametrizeConditionTimerGuard<T> PopParametrizeConditionTimerGuard<T>() =>
            GetParametrizeConditionTimerGuardPool<T>().Pop() as ParametrizeConditionTimerGuard<T>;
        internal void ReleaseParametrizeConditionTimerGuard<T>(ParametrizeConditionTimerGuard<T> guard)
            => GetParametrizeConditionTimerGuardPool<T>().Release(guard);
        
        private ObjectPool<TimerGuard> GetParametrizeConditionTimerGuardPool<T>()
        {
            var type = typeof(T);
            if (parametrizeConditionTimerGuardPools.TryGetValue(type, out var pool)) return pool;
            pool = new ObjectPool<TimerGuard>(
                50,
                data => data.Reset(),
                () => new ParametrizeConditionTimerGuard<T>()
            );
            parametrizeConditionTimerGuardPools[type] = pool;
            return pool;
        }
        
        // internal void ReleaseGuard(TimerGuard guard)
        // {
        //     switch (guard)
        //     {
        //         case GameObjectTimerGuard gameObjectTimerGuard:
        //             gameObjectTimerGuardPool.Release(gameObjectTimerGuard);
        //             break;
        //         case ConditionTimerGuard conditionTimerGuard:
        //             conditionTimerGuardPool.Release(conditionTimerGuard);
        //             break;
        //     }
        // }


        public void Dispose()
        {
            internalTimerPool.Dispose();
            timerInvocationWithoutParameterPool.Dispose();
            gameObjectTimerGuardPool.Dispose();
            conditionTimerGuardPool.Dispose();
            timerBuilderPool.Dispose();
        }
    }
}
