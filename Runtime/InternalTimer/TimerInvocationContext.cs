using System;

namespace Damdor.Temporio
{
    /// <summary>
    /// Provides contextual information for a timer invocation.
    /// </summary>
    public readonly struct TimerInvocationContext
    {
        /// <summary>
        /// Gets how many times the timer has been invoked, including the current invocation and initial invocation.
        /// Value is starting from 0.
        /// </summary>
        public int InvocationNumber { get; }

        /// <summary>
        /// Gets the date and time at which the timer invocation occurred.
        /// </summary>
        public DateTime InvocationTime { get; }

        /// <summary>
        /// Gets the time at which the timer invocation was scheduled to occur.
        /// </summary>
        public DateTime ScheduledTime { get; }

        /// <summary>
        /// Gets the time difference between when the timer invocation occurred and when it was scheduled to occur.
        /// </summary>
        public TimeSpan InvocationDelay => InvocationTime - ScheduledTime;
        
        internal TimerInvocationContext(int invocationNumber, DateTime invocationTime, DateTime scheduledTime)
        {
            InvocationNumber = invocationNumber;
            InvocationTime = invocationTime;
            ScheduledTime = scheduledTime;
        }
    }
}