using System;
using System.Collections.Generic;

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
    public readonly struct TimerGroup : IEquatable<TimerGroup>
    {
        /// <summary>
        /// Gets a value indicating whether this timer group is alive and can still be used.
        /// </summary>
        public bool IsAlive => GetInternalTimerGroup() != null;

        /// <summary>
        /// Gets the name assigned to this timer group.
        /// </summary>
        public string Name => GetInternalTimerGroup()?.Name ?? "";

        internal readonly WeakReference<TimerManager> timerManager;
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
        public int Cancel()
        {
            var internalGroup = GetInternalTimerGroup();
            if (internalGroup == null) return 0;
            timerManager.TryGetTarget(out var manager);
            return manager!.CancelGroup(id);
        }

        /// <summary>
        /// Releases this timer group and cancels all timers associated with it.
        /// After this operation, the timer group is no longer alive and should not be used.
        /// </summary>
        public void Dispose()
        {
            var internalGroup = GetInternalTimerGroup();
            if (internalGroup == null) return;
            timerManager.TryGetTarget(out var manager);
            manager!.DisposeGroup(id);
        }

        /// <summary>
        /// Adds the timers associated with this timer group to the specified list.
        /// </summary>
        /// <param name="list">
        /// The list to receive the timers in this group.
        /// </param>
        public void GetTimersInGroup(List<Timer> list)
        {
            if (!IsAlive) return;
            timerManager.TryGetTarget(out var manager);
            manager!.GetTimersForGroup(id, list);
        }

        /// <summary>
        /// Gets the active timers associated with this timer group.
        /// </summary>
        /// <return>
        /// A list containing the timers currently associated with this timer group.
        /// </return>
        public List<Timer> GetTimersInGroup()
        {
            if (!IsAlive) return new List<Timer>();
            timerManager.TryGetTarget(out var manager);
            var list = new List<Timer>();
            manager!.GetTimersForGroup(id, list);
            return list;
        }
        
        /// <summary>
        /// Determines whether both handles identify the same timer group in the same manager,
        /// regardless of whether it is still active.
        /// </summary>
        public bool Equals(TimerGroup other) =>
            ReferenceEquals(timerManager, other.timerManager) && id == other.id;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is TimerGroup other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(timerManager, id);

        /// <summary>
        /// Determines whether two handles are equal.
        /// </summary>
        public static bool operator ==(TimerGroup left, TimerGroup right) => left.Equals(right);

        /// <summary>
        /// Determines whether two handles are different.
        /// </summary>
        public static bool operator !=(TimerGroup left, TimerGroup right) => !left.Equals(right);

        private InternalTimerGroup GetInternalTimerGroup()
        {
            return timerManager != null && timerManager.TryGetTarget(out var manager)
                ? manager.GetInternalGroupId(id)
                : null;
        }
    }
}