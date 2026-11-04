namespace Damdor.Temporio
{
    public delegate bool TimerGuardCondition();
    public delegate bool ParametrizeTimerGuardCondition<in T>(T parameter);
}