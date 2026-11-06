using System.Threading;
using UnityEngine;

namespace Damdor.Temporio
{
    internal abstract class TimerGuard
    {
        public abstract bool Check();
        internal abstract void Reset();
        internal abstract void ReturnToPool(TemporioPooling pooling);

    }
    
    internal class CancellationTokenTimeGuard : TimerGuard
    {
        private CancellationToken cancellationToken;

        internal void Setup(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
        }

        public override bool Check() => !cancellationToken.IsCancellationRequested;

        internal override void Reset()
        {
            cancellationToken = default;
        }

        internal override void ReturnToPool(TemporioPooling pooling)
        {
            pooling.ReleaseCancellationTokenTimeGuard(this);
        }
    }

    internal class GameObjectTimerGuard : TimerGuard
    {
        private GameObject gameObject;

        internal void Setup(GameObject gameObject)
        {
            this.gameObject = gameObject;
        }

        public override bool Check() => gameObject != null;

        internal override void Reset()
        {
            gameObject = null;
        }

        internal override void ReturnToPool(TemporioPooling pooling)
        {
            pooling.ReleaseGameObjectTimerGuard(this);
        }
    }

    internal class ConditionTimerGuard : TimerGuard
    {
        private TimerGuardCondition condition;
        
        internal void Setup(TimerGuardCondition condition)
        {
            this.condition = condition;
        }

        public override bool Check() => !condition();

        internal override void Reset()
        {
            condition = null;
        }

        internal override void ReturnToPool(TemporioPooling pooling)
        {
            pooling.ReleaseConditionTimerGuard(this);
        }
    }
    
    internal class ParametrizeConditionTimerGuard<T> : TimerGuard
    {
        private ParametrizeTimerGuardCondition<T> condition;
        private T parameter;
        
        internal void Setup(ParametrizeTimerGuardCondition<T> condition, T parameter)
        {
            this.condition = condition;
            this.parameter = parameter;
        }

        public override bool Check() => !condition(parameter);

        internal override void Reset()
        {
            condition = null;
            parameter = default;
        }

        internal override void ReturnToPool(TemporioPooling pooling)
        {
            pooling.ReleaseParametrizeConditionTimerGuard(this);
        }
    }
    
}
