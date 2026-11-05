using System;
using System.Collections.Generic;

namespace Damdor.Temporio
{
    /// This class should be pooled and reused
    internal class InternalTimer
    {
        public long InternalId { get; set; }
        public DateTime FirstInvocationTime { get; set; }
        public TimeSpan Period { get; set; }
        public PlayerLoopTiming Timing { get; set; }
        public List<TimerInvocation> Invocations { get; } = new();
        public List<TimerGuard> Guards { get; } = new();
        public MissingPeriodBehaviour MissingPeriodBehaviour { get; set; }
        
        internal void Reset()
        {
        }
    }
}