using System;
using System.Collections.Generic;

namespace Damdor.Temporio
{
    internal class InternalTimerBuilder
    {
        public long InternalId { get; set; }
        public bool HasStartTime { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsRepeating { get; set; }
        public TimeSpan RepeatInterval { get; set; }
        public List<TimerInvocation> Invocations { get; } = new();
        public List<TimerGuard> Guards { get; } = new();
        public string DebugName { get; set; }
        public int InvocationLimit { get; set; }
        public MissingPeriodBehaviour MissingPeriodBehaviour { get; set; }
        public PlayerLoopTiming Timing { get; set; }
        
        internal void Reset()
        {
            InternalId = 0;
            StartTime = default;
            HasStartTime = false;
            IsRepeating = false;
            RepeatInterval = TimeSpan.Zero;
            Invocations.Clear();
            Guards.Clear();
            DebugName = null;
            InvocationLimit = 0;
            MissingPeriodBehaviour = MissingPeriodBehaviour.RunOnce;
            Timing = PlayerLoopTiming.Update;
        }
    }
}
