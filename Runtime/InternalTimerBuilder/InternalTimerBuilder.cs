using System;
using System.Collections.Generic;

namespace Damdor.Temporio
{
    internal class InternalTimerBuilder
    {
        public long InternalId { get; }
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
            
        }
    }
}