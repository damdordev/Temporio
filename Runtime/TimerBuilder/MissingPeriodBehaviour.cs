namespace Damdor.Temporio
{
    /// <summary>
    /// Specifies how a repeating timer handles periods that were missed because execution was delayed or interrupted.
    /// </summary>
    /// <remarks>
    /// Use this option to control whether a timer invokes only once after a delay or invokes once for each elapsed period that was missed.
    /// The default behavior is <see cref="MissingPeriodBehaviour.RunOnce"/>.
    /// </remarks>
    public enum MissingPeriodBehaviour
    {
        /// <summary>
        /// Invokes the timer callback only once after one or more periods were missed.
        /// </summary>
        RunOnce,

        /// <summary>
        /// Invokes the timer callback for every period that was missed while execution was delayed or interrupted.
        /// </summary>
        RunAll
    }
}