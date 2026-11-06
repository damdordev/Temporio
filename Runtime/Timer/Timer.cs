using System;

namespace Damdor.Temporio
{
    /// <summary>
    /// Represents a lightweight, immutable handle to a timer managed by a <see cref="TimerManager"/>.
    /// </summary>
    /// <remarks>
    /// A timer handle can be used to inspect the runtime state of the associated timer and request lifecycle
    /// operations such as cancellation. The handle does not own the timer manager and does not keep it alive.
    /// State queries return false and set their output to the default value when the timer is no longer alive
    /// or the associated manager is no longer available. Output values are meaningful only when a query succeeds.
    /// </remarks>
    public readonly struct Timer : IEquatable<Timer>
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
        /// Attempts to get the timer group associated with this timer.
        /// </summary>
        /// <param name="timerGroup">The group that contains the timer if successful; otherwise, the default value.</param>
        /// <returns>True if the timer is alive and its manager is available; otherwise, false.</returns>
        public bool TryGetTimerGroup(out TimerGroup timerGroup)
        {
            var internalTimer = GetInternalTimer();
            timerGroup = internalTimer != null ? new TimerGroup(timerManager, internalTimer.GroupId) : default;
            return internalTimer != null;
        }

        /// <summary>
        /// Attempts to get a value indicating whether the associated timer repeats.
        /// </summary>
        /// <param name="isRepeating">Whether the timer is configured to repeat if successful; otherwise, the default value.</param>
        /// <returns>True if the timer is alive and its manager is available; otherwise, false.</returns>
        public bool TryGetIsRepeating(out bool isRepeating)
        {
            var internalTimer = GetInternalTimer();
            isRepeating = internalTimer?.IsRepeating ?? default;
            return internalTimer != null;
        }

        /// <summary>
        /// Attempts to get the interval between invocations of the associated timer.
        /// </summary>
        /// <param name="repeatInterval">The repeat interval; zero for a timer configured to run once if successful; otherwise, the default value.</param>
        /// <returns>True if the timer is alive and its manager is available; otherwise, false.</returns>
        public bool TryGetRepeatInterval(out TimeSpan repeatInterval)
        {
            var internalTimer = GetInternalTimer();
            repeatInterval = internalTimer?.RepeatInterval ?? default;
            return internalTimer != null;
        }

        /// <summary>
        /// Attempts to get the next scheduled invocation time of the associated timer.
        /// </summary>
        /// <param name="nextInvocationTime">The next scheduled invocation time if successful; otherwise, the default value.</param>
        /// <returns>True if the timer is alive and its manager is available; otherwise, false.</returns>
        /// <remarks>While paused, this time remains unchanged until execution resumes, when it shifts by the total pause duration.</remarks>
        public bool TryGetNextInvocationTime(out DateTime nextInvocationTime)
        {
            var internalTimer = GetInternalTimer();
            nextInvocationTime = internalTimer?.NextInvocationTime ?? default;
            return internalTimer != null;
        }

        /// <summary>
        /// Attempts to get the remaining time until the next invocation of the associated timer.
        /// </summary>
        /// <param name="timeToNextInvocation">The remaining delay, clamped to zero when overdue if successful; otherwise, the default value.</param>
        /// <returns>True if the timer is alive and its manager is available; otherwise, false.</returns>
        /// <remarks>The remaining delay is frozen while the timer is paused directly or by its group.</remarks>
        public bool TryGetTimeToNextInvocation(out TimeSpan timeToNextInvocation)
        {
            timeToNextInvocation = default;
            if (timerManager == null || !timerManager.TryGetTarget(out var manager)) return false;
            var internalTimer = manager.GetInternalTimer(id);
            if (internalTimer == null) return false;
            var time = internalTimer.NextInvocationTime - (internalTimer.IsPaused ? internalTimer.PausedAt : manager.Now);
            timeToNextInvocation = time < TimeSpan.Zero ? TimeSpan.Zero : time;
            return true;
        }

        /// <summary>
        /// Attempts to get the configured maximum number of invocations of the associated timer.
        /// </summary>
        /// <param name="invocationLimit">The invocation limit; zero means unlimited invocations if successful; otherwise, the default value.</param>
        /// <returns>True if the timer is alive and its manager is available; otherwise, false.</returns>
        /// <remarks>This limit does not indicate how many invocations have already occurred.</remarks>
        public bool TryGetInvocationLimit(out int invocationLimit)
        {
            var internalTimer = GetInternalTimer();
            invocationLimit = internalTimer?.InvocationLimit ?? default;
            return internalTimer != null;
        }

        /// <summary>
        /// Attempts to get the number of times the associated timer has been invoked.
        /// </summary>
        /// <param name="invocationCount">The number of completed invocations if successful; otherwise, the default value.</param>
        /// <returns>True if the timer is alive and its manager is available; otherwise, false.</returns>
        public bool TryGetInvocationCount(out int invocationCount)
        {
            var internalTimer = GetInternalTimer();
            invocationCount = internalTimer?.InvocationCount ?? default;
            return internalTimer != null;
        }

        /// <summary>
        /// Attempts to get a value indicating whether the associated timer is paused directly.
        /// </summary>
        /// <param name="isPausedSelf">Whether the timer is paused by its own pause state if successful; otherwise, the default value.</param>
        /// <returns>True if the timer is alive and its manager is available; otherwise, false.</returns>
        /// <remarks>This reports only the individual pause state. A paused group can still prevent execution.</remarks>
        public bool TryGetIsPausedSelf(out bool isPausedSelf)
        {
            var internalTimer = GetInternalTimer();
            isPausedSelf = internalTimer?.IsPausedSelf ?? default;
            return internalTimer != null;
        }

        /// <summary>
        /// Attempts to get a value indicating whether the associated timer is paused directly or by its group.
        /// </summary>
        /// <param name="isPaused">Whether either the timer or its group is paused if successful; otherwise, the default value.</param>
        /// <returns>True if the timer is alive and its manager is available; otherwise, false.</returns>
        /// <remarks>Both the individual and group pause states must be cleared before execution resumes.</remarks>
        public bool TryGetIsPaused(out bool isPaused)
        {
            var internalTimer = GetInternalTimer();
            isPaused = internalTimer?.IsPaused ?? default;
            return internalTimer != null;
        }

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

        /// <summary>
        /// Pauses the timer associated with this handle, preventing it from invoking until it is resumed.
        /// Time of the next invocation will be modified to reflect the pause duration.
        /// Mind that the timer can be paused in two ways: by calling Pause() on it or on its group. 
        /// If the timer is already paused directly, cancelled, completed, or unavailable, this call has no effect.
        /// If called from an action, the remaining actions in that invocation still execute.
        /// </summary>
        public void Pause()
        {
            if (timerManager != null && timerManager.TryGetTarget(out var manager)) manager.SetTimerPaused(id, true);
        }

        /// <summary>
        /// Resumes the timer associated with this handle if it is currently paused.
        /// If the timer is not alive, is not paused, or its manager is unavailable, the call has no effect.
        /// </summary>
        /// <remarks>This clears only the timer's individual pause state. A paused group still prevents execution.</remarks>
        public void Resume()
        {
            if (timerManager != null && timerManager.TryGetTarget(out var manager)) manager.SetTimerPaused(id, false);
        }
        
        /// <summary>
        /// Determines whether both handles identify the same timer in the same manager,
        /// regardless of whether it is still active.
        /// </summary>
        public bool Equals(Timer other) =>
            ReferenceEquals(timerManager, other.timerManager) && id == other.id;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Timer other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(timerManager, id);

        /// <summary>
        /// Determines whether two handles are equal.
        /// </summary>
        public static bool operator ==(Timer left, Timer right) => left.Equals(right);

        /// <summary>
        /// Determines whether two handles are different.
        /// </summary>
        public static bool operator !=(Timer left, Timer right) => !left.Equals(right);

        private InternalTimer GetInternalTimer()
        {
            return timerManager != null && timerManager.TryGetTarget(out var manager)
                ? manager.GetInternalTimer(id)
                : null;
        }
        
    }
}
