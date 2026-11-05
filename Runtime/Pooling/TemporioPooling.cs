using System;
using System.Collections.Generic;

namespace Damdor.Temporio
{
    internal class TemporioPooling : IDisposable
    {
        private readonly ObjectPool<InternalTimer> internalTimerPool =
            new(500, data => data.Reset(), () => new InternalTimer());
        private readonly ObjectPool<TimerInvocationWithoutParameter> timerInvocationDataWithoutParameterPool =
            new(500, data => data.Reset(), () => new TimerInvocationWithoutParameter());
        private readonly Dictionary<Type, ObjectPool<TimerInvocationWithParameter>> timerInvocationDataWithParameterPools =
            new();
        private readonly ObjectPool<InternalTimerBuilder> timerBuilderPool =
            new(50, data => data.Reset(), () => new InternalTimerBuilder());
        private readonly ObjectPool<GameObjectTimerGuard> gameObjectTimerGuardPool =
            new(100, data => data.Reset(), () => new GameObjectTimerGuard());
        private readonly ObjectPool<ConditionTimerGuard> conditionTimerGuardPool =
            new(100, data => data.Reset(), () => new ConditionTimerGuard());
        private readonly Dictionary<Type, ObjectPool<ParametrizeConditionTimerGuard>> parametrizeConditionTimerGuardPools =
            new();

        internal InternalTimer PopInternalTimer() => internalTimerPool.Pop();
        internal void ReleaseInternalTimer(InternalTimer data) => internalTimerPool.Release(data);
        
        internal TimerInvocationWithoutParameter PopTimerInvocationDataWithoutParameter() => new();
        internal void ReleaseTimerInvocationDataWithoutParameter(TimerInvocationWithoutParameter invocation) {}
        
        internal TimerInvocationWithParameter<T> PopTimerInvocationDataWithParameter<T>() => new();
        internal void ReleaseTimerInvocationDataWithParameter(TimerInvocationWithParameter invocation) {}
        
        internal InternalTimerBuilder PopInternalTimerBuilder() => timerBuilderPool.Pop();
        internal void ReleaseInternalTimerBuilder(InternalTimerBuilder timerBuilder) => timerBuilderPool.Release(timerBuilder);
        
        internal GameObjectTimerGuard PopGameObjectTimerGuard() => new GameObjectTimerGuard();
        internal void ReleaseGameObjectTimerGuard(GameObjectTimerGuard guard) {}
        
        internal ConditionTimerGuard PopConditionTimerGuard() => new ConditionTimerGuard();
        internal void ReleaseConditionTimerGuard(ConditionTimerGuard guard) {}
        
        internal ParametrizeConditionTimerGuard<T> PopParametrizeConditionTimerGuard<T>() => new();
        internal void ReleaseParametrizeConditionTimerGuard(ParametrizeConditionTimerGuard guard) {}

        public void Dispose()
        {
        }
    }
}
