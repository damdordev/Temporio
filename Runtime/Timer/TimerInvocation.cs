namespace Damdor.Temporio
{
    internal abstract class TimerInvocation
    {
        public abstract void Fire(TimerInvocationContext context);

        internal abstract void ReturnToPool(TemporioPooling pooling);
        internal abstract void Reset();
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

        internal override void ReturnToPool(TemporioPooling pooling)
        {
            pooling.ReleaseTimerInvocationWithoutParameter(this);
        }

        internal override void Reset()
        {
            Action = null;
            ActionWithContext = null;
        }
        
    }
    
    internal class TimerInvocationWithParameter<T> : TimerInvocation
    {
        public TimerActionWithParameter<T> Action { get; set; }
        public TimerActionWithContextAndParameter<T> ActionWithContext { get; set; }
        public T Parameter { get; set; }
        
        public override void Fire(TimerInvocationContext context)
        {
            Action?.Invoke(Parameter);
            ActionWithContext?.Invoke(context, Parameter);
        }

        internal override void ReturnToPool(TemporioPooling pooling)
        {
            pooling.ReleaseTimerInvocationWithParameter(this);
        }

        internal override void Reset()
        {
            Action = null;
            ActionWithContext = null;
            Parameter = default;
        }
    }
}
