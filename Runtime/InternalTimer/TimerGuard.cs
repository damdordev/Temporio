using System;
using UnityEngine;

namespace Damdor.Temporio
{
    internal abstract class TimerGuard
    {
        public abstract bool Check();
    }
    
    internal class GameObjectTimerGuard : TimerGuard
    {
        private GameObject gameObject;

        internal void Setup(GameObject gameObject)
        {
            this.gameObject = gameObject;
        }

        public override bool Check() => gameObject != null;

        internal void Reset()
        {
            gameObject = null;
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

        internal void Reset()
        {
            condition = null;
        }
    }

    internal abstract class ParametrizeConditionTimerGuard : TimerGuard
    {
        // ReSharper disable once UnusedMember.Global
        public abstract Type ParameterType { get; }
        // ReSharper disable once UnusedMemberHierarchy.Global
        internal virtual void Reset() {}
    }
        
    
    internal class ParametrizeConditionTimerGuard<T> : ParametrizeConditionTimerGuard
    {
        public override Type ParameterType => typeof(T);
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
            base.Reset();
            condition = null;
            parameter = default;
        }
    }
    
}