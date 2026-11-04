using System;

namespace Damdor.Temporio
{
    /// <summary>
    /// Provides update callbacks for one or more player loop phases.
    /// </summary>
    /// <remarks>
    /// Implementations are responsible for driving registered receivers at the requested
    /// <see cref="PlayerLoopTiming"/> and for releasing any loop integration or related
    /// resources when disposed.
    /// </remarks>
    public interface IUpdateProvider : IDisposable
    {
        /// <summary>
        /// Registers an update receiver to be invoked during the specified player loop phase.
        /// </summary>
        /// <param name="timing">The player loop phase in which the receiver should be invoked.</param>
        /// <param name="receiver">The callback to invoke during the specified loop phase.</param>
        void Register(PlayerLoopTiming timing, UpdateReceiver receiver);

        /// <summary>
        /// Unregisters an update receiver from the specified player loop phase.
        /// </summary>
        /// <param name="timing">The player loop phase from which the receiver should be removed.</param>
        /// <param name="receiver">The callback to remove from the specified loop phase.</param>
        void Unregister(PlayerLoopTiming timing, UpdateReceiver receiver);
    }
}