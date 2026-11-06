using System;

namespace Damdor.Temporio
{
    /// <summary>
    /// Represents a lightweight, immutable handle to a timer managed by a <see cref="TimerManager"/>.
    /// </summary>
    /// <remarks>
    /// A timer handle can be used to inspect the runtime state of the associated timer and request lifecycle
    /// operations such as cancellation. The handle does not own the timer manager and does not keep it alive.
    /// If the associated manager is no longer available, timer state information may be unavailable and default
    /// values may be returned by state queries.
    /// </remarks>
    public readonly struct Timer
    {
        /// <summary>
        /// Gets a value indicating whether the associated timer is currently alive.
        /// </summary>
        /// <value>
        /// true if the timer manager is available and the timer identified by this handle is alive; otherwise, false.
        /// </value>
        /// <remarks>
        /// This property returns false if the associated timer manager can no longer be reached or if the timer is no longer active.
        /// </remarks>
        public bool IsAlive => GetInternalTimer() != null;

        /// <summary>
        /// Gets the timer group associated with this timer.
        /// </summary>
        /// <value>
        /// A handle representing the group that contains the timer, or the default timer group handle if no group information is available.
        /// </value>
        /// <remarks>
        /// This property provides access to the group assigned to the timer by the timer manager. If the associated timer manager can no longer be reached or the timer is no longer available, the returned handle may represent a default or inactive group.
        /// </remarks>
        public TimerGroup TimerGroup => default;

        /// <summary>
        /// Gets a value indicating whether the associated timer is periodic.
        /// </summary>
        /// <value>
        /// true if the timer manager is available and the timer identified by this handle is periodic; otherwise, false.
        /// </value>
        /// <remarks>
        /// This property returns false if the associated timer manager can no longer be reached or if the timer is configured to run only once.
        /// </remarks>
        public bool IsRepeating => GetInternalTimer()?.IsRepeating ?? false;

        /// <summary>
        /// Gets the interval between invocations of the associated timer.
        /// </summary>
        /// <value>
        /// The timer period, or <see cref="TimeSpan.Zero"/> if the associated timer manager is unavailable.
        /// </value>
        public TimeSpan RepeatInterval => GetInternalTimer()?.RepeatInterval ?? TimeSpan.Zero;

        /// <summary>
        /// Gets the date and time at which the associated timer is next scheduled to be invoked.
        /// </summary>
        /// <value>
        /// The next scheduled invocation time for the timer, or the current date and time if the associated timer manager is unavailable.
        /// </value>
        /// <remarks>
        /// This value reflects the timer state reported by the associated timer manager. If the manager can no longer be reached, the property returns the current date and time as a fallback value.
        /// </remarks>
        public DateTime NextInvocationTime => GetInternalTimer()?.NextInvocationTime ?? DateTime.Now;

        /// <summary>
        /// Gets the remaining time until the associated timer is next scheduled to invoke.
        /// </summary>
        /// <value>
        /// The time interval until the next invocation, or <see cref="TimeSpan.Zero"/> if the associated timer manager is unavailable.
        /// </value>
        public TimeSpan TimeToNextInvocation
        {
            get
            {
                var internalTimer = GetInternalTimer();
                if(internalTimer == null) return TimeSpan.Zero;
                timerManager.TryGetTarget(out var manager);
                var time = internalTimer.NextInvocationTime - manager!.Now;
                return time < TimeSpan.Zero ? TimeSpan.Zero : time;
            }
        }

        /// <summary>
        /// Gets the configured maximum number of times the associated timer may be invoked.
        /// </summary>
        /// <value>
        /// The invocation limit reported by the timer manager, or -1 if the associated timer manager is unavailable.
        /// 0 means no limit
        /// </value>
        /// <remarks>
        /// This value describes the timer's configured invocation cap and does not indicate how many invocations have already occurred.
        /// </remarks>
        public int InvocationLimit => GetInternalTimer()?.InvocationLimit ?? -1;

        /// <summary>
        /// Gets the number of times the associated timer has been invoked.
        /// </summary>
        /// <value>
        /// The number of completed invocations for the timer, or 0 if the timer manager is unavailable.
        /// </value>
        /// <remarks>
        /// This property reflects the invocation count reported by the associated timer manager. If the manager can no longer be reached, the count cannot be determined and 0 is returned.
        /// </remarks>
        public int InvocationCount => GetInternalTimer()?.InvocationCount ?? 0;

        private readonly WeakReference<TimerManager> timerManager;
        internal readonly long id;
        
        internal Timer(WeakReference<TimerManager> timerManager, long id)
        {
            this.timerManager = timerManager;
            this.id = id;
        }

        /// <summary>
        /// Attempts to cancel the timer associated with this handle.
        /// If called from an action, the rest of actions in the current invocation will still be executed.
        /// </summary>
        /// <return>
        /// true if the timer was successfully cancelled; otherwise, false.
        /// </return>
        public bool Cancel() => timerManager != null && timerManager.TryGetTarget(out var manager)
            && manager.CancelTimer(id);
        
        private InternalTimer GetInternalTimer()
        {
            return timerManager != null && timerManager.TryGetTarget(out var manager)
                ? manager.GetInternalTimer(id)
                : null;
        }
        
    }
}
