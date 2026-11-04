using UnityEngine;

namespace Damdor.Temporio
{
    /// <summary>
    /// Provides an <see cref="IUpdateProvider"/> implementation backed by a Unity <c>MonoBehaviour</c>.
    /// </summary>
    /// <remarks>
    /// Creates an internal Unity <c>GameObject</c> with an update forwarding component and uses it to dispatch Update, FixedUpdate, and LateUpdate callbacks to registered receivers. Disposing the provider clears registered receivers and destroys the internal Unity object.
    /// </remarks>
    public class MonoUpdateProvider : BaseUpdateProvider
    {
        private UpdateProviderMonoBehaviour mono;

        public MonoUpdateProvider()
        {
            var gameObject = new GameObject();
            Object.DontDestroyOnLoad(gameObject);
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            mono = gameObject.AddComponent<UpdateProviderMonoBehaviour>();
            mono.UpdateReceiver = OnUpdate;
            mono.FixedUpdateReceiver = OnFixedUpdate;
            mono.LateUpdateReceiver = OnLateUpdate;
        }
        
        public override void Dispose()
        {
            base.Dispose();
            Object.Destroy(mono.gameObject);
            mono = null;
        }
        
        private void OnUpdate() => Run(PlayerLoopTiming.Update);
        private void OnFixedUpdate() => Run(PlayerLoopTiming.FixedUpdate);
        private void OnLateUpdate() => Run(PlayerLoopTiming.LateUpdate);
    }
}