using System;
using System.Collections.Generic;

namespace Damdor.Temporio
{
    internal class TemporioPooling : IDisposable
    {
        #region InternalTimer

        private readonly ObjectPool<InternalTimer> internalTimerPool =
            new(500, data => data.Reset(), () => new InternalTimer());

        public InternalTimer PopInternalTimer() => internalTimerPool.Pop();
        public void ReleaseInternalTimer(InternalTimer data) => internalTimerPool.Release(data);
        
        #endregion

        #region InternalTimerBuilder

        private readonly ObjectPool<InternalTimerBuilder> internalTimerBuilderPool =
            new(50, data => data.Reset(), () => new InternalTimerBuilder());

        public InternalTimerBuilder PopInternalTimerBuilder() => internalTimerBuilderPool.Pop();
        public void ReleaseInternalTimerBuilder(InternalTimerBuilder timerBuilder) => internalTimerBuilderPool.Release(timerBuilder);
        
        #endregion
        
        #region Invocation

        private readonly ObjectPool<TimerInvocationWithoutParameter> timerInvocationWithoutParameterPool =
            new(500, data => data.Reset(), () => new TimerInvocationWithoutParameter());

        private readonly Dictionary<Type, ObjectPool<TimerInvocation>>
            timerInvocationWithParameterPools = new();
        
        public TimerInvocationWithoutParameter PopTimerInvocationWithoutParameter() => timerInvocationWithoutParameterPool.Pop();
        public TimerInvocationWithParameter<T> PopTimerInvocationWithParameter<T>() => GetTimerInvocationWithParameterPool<T>().Pop() as TimerInvocationWithParameter<T>;
        public void ReleaseTimerInvocationWithoutParameter(TimerInvocationWithoutParameter invocation) => timerInvocationWithoutParameterPool.Release(invocation);
        public void ReleaseTimerInvocationWithParameter<T>(TimerInvocationWithParameter<T> invocation) => GetTimerInvocationWithParameterPool<T>().Release(invocation);
        
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
        
        #endregion

        #region Guard

        private readonly ObjectPool<CancellationTokenTimeGuard> cancellationTokenTimeGuardPool =
            new(100, data => data.Reset(), () => new CancellationTokenTimeGuard());
        private readonly ObjectPool<GameObjectTimerGuard> gameObjectTimerGuardPool =
            new(100, data => data.Reset(), () => new GameObjectTimerGuard());
        private readonly ObjectPool<ConditionTimerGuard> conditionTimerGuardPool =
            new(100, data => data.Reset(), () => new ConditionTimerGuard());
        private readonly Dictionary<Type, ObjectPool<TimerGuard>>
            parametrizeConditionTimerGuardPools = new();

        public CancellationTokenTimeGuard PopCancellationTokenTimeGuard() => cancellationTokenTimeGuardPool.Pop();
        public void ReleaseCancellationTokenTimeGuard(CancellationTokenTimeGuard guard) => cancellationTokenTimeGuardPool.Release(guard);
        public GameObjectTimerGuard PopGameObjectTimerGuard() => gameObjectTimerGuardPool.Pop();
        public void ReleaseGameObjectTimerGuard(GameObjectTimerGuard guard) => gameObjectTimerGuardPool.Release(guard);
        public ConditionTimerGuard PopConditionTimerGuard() => conditionTimerGuardPool.Pop();
        public void ReleaseConditionTimerGuard(ConditionTimerGuard guard) => conditionTimerGuardPool.Release(guard);

        public ParametrizeConditionTimerGuard<T> PopParametrizeConditionTimerGuard<T>() =>
            GetParametrizeConditionTimerGuardPool<T>().Pop() as ParametrizeConditionTimerGuard<T>;
        public void ReleaseParametrizeConditionTimerGuard<T>(ParametrizeConditionTimerGuard<T> guard)
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
        
        #endregion
        
        #region InternalTimerGroup

        private readonly ObjectPool<InternalTimerGroup> internalTimerGroupPool =
            new(500, data => data.Reset(), () => new InternalTimerGroup());

        public InternalTimerGroup PopInternalTimerGroup() => internalTimerGroupPool.Pop();
        public void ReleaseInternalTimerGroup(InternalTimerGroup data) => internalTimerGroupPool.Release(data);
        
        #endregion
        
        public void Dispose()
        {
            internalTimerPool.Dispose();
            timerInvocationWithoutParameterPool.Dispose();
            cancellationTokenTimeGuardPool.Dispose();
            gameObjectTimerGuardPool.Dispose();
            conditionTimerGuardPool.Dispose();
            internalTimerBuilderPool.Dispose();
            internalTimerGroupPool.Dispose();
            foreach (var pool in parametrizeConditionTimerGuardPools.Values) pool.Dispose();
            parametrizeConditionTimerGuardPools.Clear();
            foreach (var pool in timerInvocationWithParameterPools.Values) pool.Dispose();
            timerInvocationWithParameterPools.Clear();
        }
    }
}
