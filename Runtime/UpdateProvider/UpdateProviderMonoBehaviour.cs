using UnityEngine;

namespace Damdor.Temporio
{
    /// <summary>
    /// Unity <c>MonoBehaviour</c> component used to bridge Unity player loop callbacks to update receiver delegates.
    /// </summary>
    /// <remarks>
    /// Intended for internal use by update provider implementations that need a Unity component to participate in the
    /// engine update cycle. The component stores receivers for Update, FixedUpdate, and LateUpdate dispatching so they
    /// can be invoked from the corresponding Unity callbacks.
    /// </remarks>
    public class UpdateProviderMonoBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Gets or sets the delegate invoked during the Unity Update callback.
        /// </summary>
        /// <remarks>
        /// Used to forward per-frame update execution to the assigned receiver. If no receiver is assigned, no update action is performed.
        /// </remarks>
        public UpdateReceiver UpdateReceiver { get; set; }

        /// <summary>
        /// Gets or sets the delegate invoked during the Unity FixedUpdate callback.
        /// </summary>
        /// <remarks>
        /// Forwards fixed-timestep update execution to the assigned receiver, typically for logic associated with the
        /// physics update cycle. If no receiver is assigned, no fixed update action is performed.
        /// </remarks>
        public UpdateReceiver FixedUpdateReceiver { get; set; }

        /// <summary>
        /// Gets or sets the delegate called from the Unity LateUpdate callback.
        /// </summary>
        /// <remarks>
        /// Enables dispatching late-frame processing to the assigned receiver after regular update handling has completed.
        /// If unset, the callback performs no action.
        /// </remarks>
        public UpdateReceiver LateUpdateReceiver { get; set; }
        
        private void Update()
        {
            UpdateReceiver?.Invoke();
        }

        private void FixedUpdate()
        {
            FixedUpdateReceiver?.Invoke();
        }

        private void LateUpdate()
        {
            LateUpdateReceiver?.Invoke();
        }
    }
}