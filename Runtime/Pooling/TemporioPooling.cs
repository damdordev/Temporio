using System;

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

        internal InternalTimer PopInternalTimer() => internalTimerPool.Pop();
        internal void ReleaseInternalTimer(InternalTimer data) => internalTimerPool.Release(data);

        internal TimerInvocationWithoutParameter PopTimerInvocationWithoutParameter() => timerInvocationWithoutParameterPool.Pop();
        // ReSharper disable once MemberCanBeMadeStatic.Global
        internal TimerInvocationWithParameter<T> PopTimerInvocationWithParameter<T>() => new();
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
        internal ConditionTimerGuard PopConditionTimerGuard() => conditionTimerGuardPool.Pop();
        // ReSharper disable once MemberCanBeMadeStatic.Global
        internal ParametrizeConditionTimerGuard<T> PopParametrizeConditionTimerGuard<T>() => new();
        internal void ReleaseGuard(TimerGuard guard)
        {
            switch (guard)
            {
                case GameObjectTimerGuard gameObjectTimerGuard:
                    gameObjectTimerGuardPool.Release(gameObjectTimerGuard);
                    break;
                case ConditionTimerGuard conditionTimerGuard:
                    conditionTimerGuardPool.Release(conditionTimerGuard);
                    break;
            }
        }


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
