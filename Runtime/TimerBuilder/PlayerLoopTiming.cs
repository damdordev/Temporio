namespace Damdor.Temporio
{
    /// <summary>
    /// Specifies the player loop phase in which update receivers and timers are processed.
    /// </summary>
    /// <remarks>
    /// Use this value to select whether scheduled work is executed during the regular update,
    /// fixed update, or late update phase of the application loop.
    /// </remarks>
    public enum PlayerLoopTiming
    {
        /// <summary>
        /// Processes update timers during the regular update phase of the player loop.
        /// </summary>
        Update,

        /// <summary>
        /// Processes update timers during the fixed update phase of the player loop.
        /// </summary>
        FixedUpdate,

        /// <summary>
        /// Processes update timers during the late update phase of the player loop.
        /// </summary>
        LateUpdate
    }
}