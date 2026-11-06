using System;
using System.Collections.Generic;

namespace Damdor.Temporio
{
    /// This class should be pooled and reused
    internal class InternalTimer
    {
        public long Id { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan RepeatInterval { get; set; }
        public DateTime NextInvocationTime { get; set; }
        public bool IsRepeating { get; set; }
        public PlayerLoopTiming Timing { get; set; }
        public List<TimerInvocation> Invocations { get; } = new();
        public List<TimerGuard> Guards { get; } = new();
        public MissingPeriodBehaviour MissingPeriodBehaviour { get; set; }
        public int InvocationLimit { get; set; }
        public int InvocationCount { get; set; }
        public string DebugName { get; set; }
        public long GroupId { get; set; }
        public bool IsPausedSelf { get; set; }
        public bool IsPausedByGroup { get; set; }
        public bool IsPaused => IsPausedSelf || IsPausedByGroup;
        public DateTime PausedAt { get; set; }
        
        internal void Reset()
        {
            Id = 0;
            StartTime = default;
            RepeatInterval = TimeSpan.Zero;
            NextInvocationTime = default;
            IsRepeating = false;
            Timing = PlayerLoopTiming.Update;
            Invocations.Clear();
            Guards.Clear();
            MissingPeriodBehaviour = MissingPeriodBehaviour.RunOnce;
            InvocationLimit = 0;
            InvocationCount = 0;
            DebugName = null;
            IsPausedSelf = false;
            IsPausedByGroup = false;
            PausedAt = default;
        }
    }
}
