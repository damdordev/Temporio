using System;

namespace Damdor.Temporio
{
    /// <summary>
    /// Provides the current local system date and time for timer and scheduling components.
    /// </summary>
    /// <remarks>
    /// This implementation reads the current value from the local system clock.
    /// </remarks>
    public class LocalTimeProvider : ITimeProvider
    {
        public DateTime Now => DateTime.Now;

        public void Dispose() { }
    }
}