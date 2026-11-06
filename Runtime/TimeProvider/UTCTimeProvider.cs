using System;

namespace Damdor.Temporio
{
    /// <summary>
    /// Provides the current date and time in Coordinated Universal Time.
    /// </summary>
    /// <remarks>
    /// Implements a time source that returns the system UTC time, suitable for components that require a consistent, time-zone-independent reference.
    /// </remarks>
    public class UTCTimeProvider : ITimeProvider
    {
        public DateTime Now => DateTime.UtcNow;
    }
}