namespace Damdor.Temporio
{
    public delegate void TimerAction();
    public delegate void TimerActionWithContext(TimerInvocationContext context);
    public delegate void TimerActionWithParameter<in T>(T parameter);
    public delegate void TimerActionWithContextAndParameter<in T>(TimerInvocationContext context, T parameter);
}