using System.Collections.Generic;

namespace Damdor.Temporio
{
    /// <summary>
    /// Provides a reusable base implementation of <see cref="IUpdateProvider"/> that stores update receivers by player loop timing.
    /// </summary>
    /// <remarks>
    /// Derived update providers are responsible for integrating with an application loop and invoking registered receivers for the appropriate <see cref="PlayerLoopTiming"/>. Receivers are maintained per timing and are cleared when the provider is disposed.
    /// </remarks>
    public class BaseUpdateProvider : IUpdateProvider
    {
        private readonly Dictionary<PlayerLoopTiming, List<UpdateReceiver>> receivers = new();
        
        public void Register(PlayerLoopTiming timing, UpdateReceiver receiver)
        {
            GetReceivers(timing).Add(receiver);
        }

        public void Unregister(PlayerLoopTiming timing, UpdateReceiver receiver)
        {
            GetReceivers(timing).Remove(receiver);
        }

        private List<UpdateReceiver> GetReceivers(PlayerLoopTiming timing)
        {
            if (receivers.TryGetValue(timing, out var receiversForTiming)) return receiversForTiming;
            receiversForTiming = new List<UpdateReceiver>();
            receivers.Add(timing, receiversForTiming);
            return receiversForTiming;
        }

        /// <summary>
        /// Invokes all update receivers registered for the specified player loop timing.
        /// </summary>
        /// <param name="timing">The player loop timing whose registered receivers should be invoked.</param>
        protected void Run(PlayerLoopTiming timing)
        {
            foreach (var receiver in GetReceivers(timing))
            {
                receiver();
            }
        }

        public virtual void Dispose()
        {
            receivers.Clear();
        }
    }
}