namespace Damdor.Temporio
{
    /// <summary>
    /// Provides an update provider whose registered receivers are invoked manually.
    /// </summary>
    /// <remarks>
    /// Use this provider when update execution must be controlled explicitly rather than being driven automatically by
    /// an application or engine loop. Receivers are registered per <see cref="PlayerLoopTiming"/> through the
    /// inherited registration methods and are invoked when the corresponding timing is ticked.
    /// </remarks>
    public class ManualUpdateProvider : BaseUpdateProvider
    {
        /// <summary>
        /// Invokes all registered update receivers for the specified player loop timing.
        /// </summary>
        /// <param name="timing">The player loop phase whose registered receivers should be processed.</param>
        public void Tick(PlayerLoopTiming timing) => Run(timing);
    }
}