using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Damdor.Temporio
{
    /// <summary>
    /// Represents a logical grouping of timers created by a <see cref="TimerManager"/>.
    /// </summary>
    /// <remarks>
    /// A timer group can be used to associate multiple scheduled timers with a shared identity,
    /// allowing grouped operations such as cancellation or disposal to be applied to all timers
    /// in the group. Timer groups are tied to the manager that created them and should only be
    /// used with timers scheduled by that same manager.
    /// </remarks>
    public readonly struct TimerGroup
    {
        /// <summary>
        /// Gets a value indicating whether this timer group is alive and can still be used.
        /// </summary>
        public bool IsAlive => false;

        /// <summary>
        /// Gets the name assigned to this timer group.
        /// </summary>
        public string Name => "";

        private readonly WeakReference<TimerManager> timerManager;
        internal readonly long id;
        
        internal TimerGroup(WeakReference<TimerManager> timerManager, long id)
        {
            this.timerManager = timerManager;
            this.id = id;
        }

        /// <summary>
        /// Cancels all active timers associated with this timer group.
        /// </summary>
        /// <return>
        /// The number of timers that were canceled.
        /// </return>
        public int Cancel() => 0;

        /// <summary>
        /// Releases this timer group and cancels all timers associated with it.
        /// After this operation, the timer group is no longer alive and should not be used.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Adds the timers associated with this timer group to the specified list.
        /// </summary>
        /// <param name="list">
        /// The list to receive the timers in this group.
        /// </param>
        public void GetTimersInGroup(List<Timer> list)
        {
        }

        /// <summary>
        /// Gets the active timers associated with this timer group.
        /// </summary>
        /// <return>
        /// A list containing the timers currently associated with this timer group.
        /// </return>
        public List<Timer> GetTimersInGroup() => null;
    }
}