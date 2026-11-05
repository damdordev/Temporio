using System;

namespace Damdor.Temporio
{
    /// <summary>
    /// Provides a manually controlled implementation of <see cref="ITimeProvider"/>.
    /// </summary>
    /// <remarks>
    /// Use this time provider when the current date and time must be supplied explicitly,
    /// such as in tests, simulations, or deterministic scheduling scenarios. The reported
    /// time is controlled through the provider rather than read from the system clock.
    /// </remarks>
    public class ManualTimeProvider : ITimeProvider
    {
        public DateTime Now { get; set; }
        
        public ManualTimeProvider(DateTime now)
        {
            Now = now;
        }
    }
}