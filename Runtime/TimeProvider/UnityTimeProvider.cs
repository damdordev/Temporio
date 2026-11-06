using System;
using UnityEngine;

namespace Damdor.Temporio
{
    /// <summary>
    /// Provides a Unity-based source of current date and time for timer and scheduling components.
    /// </summary>
    /// <remarks>
    /// Captures the system date and time together with Unity's elapsed time at creation, then reports time by adding
    /// the elapsed Unity time to that initial value. Depending on configuration, time progression can follow Unity's
    /// scaled game time or unscaled real time since startup.
    /// </remarks>
    public class UnityTimeProvider : ITimeProvider
    {
        public virtual DateTime Now => startDateTime + TimeSpan.FromSeconds(GetUnityTime() - startUnityTime);

        private readonly DateTime startDateTime;
        private readonly double startUnityTime;
        private readonly bool useScaledTime;

        // ReSharper disable once MemberCanBeProtected.Global
        public UnityTimeProvider(bool useScaledTime = true)
        {
            startDateTime = DateTime.Now;
            this.useScaledTime = useScaledTime;
            startUnityTime = GetUnityTime();
        }
        
        private double GetUnityTime() => useScaledTime ? Time.timeAsDouble : Time.realtimeSinceStartupAsDouble;
    }
}