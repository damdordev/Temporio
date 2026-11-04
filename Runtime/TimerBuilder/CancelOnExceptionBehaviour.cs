namespace Damdor.Temporio
{
    /// <summary>
    /// Specifies if the timer should be canceled when an exception occurs.
    /// </summary>
    public enum CancelOnExceptionBehaviour
    {
        /// <summary>
        /// Uses the inherited cancel-on-exception behavior from <see cref="TimerManager"/>
        /// </summary>
        Inherit,
        
        /// <summary>
        /// Timer will be canceled when an exception occurs. Next invocations will not be scheduled.
        /// Actions in the current invocation will still be executed.
        /// </summary>
        Cancel,

        /// <summary>
        /// Keeps the timer active when an exception occurs. Next invocations will be scheduled.
        /// Actions in the current invocation will still be executed.
        /// </summary>
        DontCancel,
    }
}