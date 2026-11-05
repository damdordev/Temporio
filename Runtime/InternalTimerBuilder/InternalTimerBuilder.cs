using System;
using System.Collections.Generic;

namespace Damdor.Temporio
{
    internal class InternalTimerBuilder
    {
        public long InstanceId { get; }
        public DateTime StartTime { get; set; }
        public bool IsRepeating { get; set; }
        public TimeSpan RepeatInterval { get; set; }
        public List<TimerInvocation> Invocations { get; } = new();
        public List<TimerGuard> Guards { get; } = new();
        public string DebugName { get; set; }
        public int InvocationLimit { get; set; }
        
        internal void Reset()
        {
            
        }
    }
}