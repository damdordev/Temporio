using System;

namespace Damdor.Temporio
{
    internal abstract class TimerInvocation
    {
        public abstract void Fire(TimerInvocationContext context);
    }
    
    internal class TimerInvocationWithoutParameter : TimerInvocation
    {
        public TimerAction Action { get; set; }
        public TimerActionWithContext ActionWithContext { get; set; }
        
        public override void Fire(TimerInvocationContext context)
        {
            Action?.Invoke();
            ActionWithContext?.Invoke(context);
        }
        
        internal void Reset()
        {
            Action = null;
            ActionWithContext = null;
        }
    }

    internal abstract class TimerInvocationWithParameter : TimerInvocation
    {
        public abstract Type ParameterType { get; }
    }
    
    internal class TimerInvocationWithParameter<T> : TimerInvocationWithParameter
    {
        public override Type ParameterType => typeof(T);
        public TimerActionWithParameter<T> Action { get; set; }
        public TimerActionWithContextAndParameter<T> ActionWithContext { get; set; }
        public T Parameter { get; set; }
        
        public override void Fire(TimerInvocationContext context)
        {
            Action?.Invoke(Parameter);
            ActionWithContext?.Invoke(context, Parameter);
        }
        
        internal void Reset()
        {
            Action = null;
            ActionWithContext = null;
            Parameter = default;
        }
    }
}
