using System;
using System.Collections.Generic;

namespace Damdor.Temporio
{
    /// <summary>
    /// Manages timer-related functionalities, providing an infrastructure for scheduling, running,
    /// and releasing timers within a Unity- or application-specific context. This class integrates
    /// with an update provider to handle execution updates.
    /// </summary>
    public class TimerManager : IDisposable
    {
        /// <summary>
        /// Current time for this timer manager. Value is taken from <see cref="ITimeProvider"/>.
        /// </summary>
        public DateTime Now => timeProvider.Now;
        
        internal TemporioPooling Pooling { get; }
        
        private readonly WeakReference<TimerManager> weakReference;
        private readonly ITimeProvider timeProvider;
        private readonly IUpdateProvider updateProvider;

        private readonly Dictionary<long, InternalTimerBuilder> timerBuilders = new();
        private long lastTimerBuilderInternalId;
        private bool isDisposed;
        
        /// <summary>
        /// Creates a new instance of <see cref="TimerManager"/>. Both <see cref="ITimeProvider"/> and <see cref="IUpdateProvider"/>
        /// become owned by this instance and will be disposed when this instance is disposed.
        /// </summary>
        /// <param name="timeProvider">Provider for current time.</param>
        /// <param name="updateProvider">Provider for update events. If null, the default one is used.</param>
        /// <exception cref="ArgumentException">Thrown if any parameter is null.</exception>
        public TimerManager(ITimeProvider timeProvider, IUpdateProvider updateProvider)
        {
            this.timeProvider = timeProvider;
            this.updateProvider = updateProvider;
            weakReference = new WeakReference<TimerManager>(this);
            Pooling = new TemporioPooling();
        }

        /// <summary>
        /// Starts TimerManager. Since then you can use the timer manager to schedule and manage timers.
        /// <remarks>TimerManager cannot be run twice</remarks>
        /// </summary>
        public void Run()
        {
            updateProvider.Register(PlayerLoopTiming.Update, OnUpdate);
            updateProvider.Register(PlayerLoopTiming.FixedUpdate, OnFixedUpdate);
            updateProvider.Register(PlayerLoopTiming.LateUpdate, OnLateUpdate);
        }

        /// <summary>
        /// Releases the TimerManager, unregistering all update event handlers and stopping
        /// further timer management. After calling this method, the TimerManager will no longer
        /// respond to updates or manage timers.
        /// </summary>
        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            ClearTimerBuilders();
            updateProvider.Unregister(PlayerLoopTiming.Update, OnUpdate);
            updateProvider.Unregister(PlayerLoopTiming.FixedUpdate, OnFixedUpdate);
            updateProvider.Unregister(PlayerLoopTiming.LateUpdate, OnLateUpdate);
            
            updateProvider.Dispose();
            timeProvider.Dispose();
            Pooling.Dispose();
        }

        /// <summary>
        /// Creates a new timer using the <see cref="TimerBuilder"/> class.
        /// </summary>
        /// <returns>A <see cref="TimerBuilder"/> instance to configure and run the timer.</returns>
        public TimerBuilder Schedule()
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(TimerManager));
            var internalId = checked(++lastTimerBuilderInternalId);
            CreateNewInternalTimerBuilder(internalId);
            var builder = new TimerBuilder();
            builder.Setup(weakReference, internalId);
            return builder;
        }

        /// <summary>
        /// Sets a custom error handler to manage exceptions that occur during timer operations.
        /// </summary>
        /// <param name="errorHandler">The handler to be invoked when an error is encountered.
        /// This delegate receives the debug name of the timer and the exception that occurred.</param>
        public void SetErrorHandler(TimerExceptionHandler errorHandler)
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether errors occurring during timer execution
        /// should be logged. When enabled, relevant error details are logged for diagnostic purposes.
        /// </summary>
        public bool LogErrors { get; set; } = true;

        /// <summary>
        /// Indicates whether a timer is canceled when an exception occurs during its invocation.
        /// Affects only timers that are periodic.
        /// Actions in the current invocation will still be executed.
        /// </summary>
        public bool CancelTimerOnException { get; set; } = false;
        
        internal bool IsAlive(long internalTimerId) => true;
        internal bool IsPeriodic(long internalTimerId) => false;
        internal TimeSpan GetPeriod(long internalTimerId) => TimeSpan.Zero;
        internal DateTime GetNextInvocationTime(long internalTimerId) => DateTime.Now;
        internal TimeSpan GetTimeToNextInvocation(long internalTimerId) => TimeSpan.Zero;
        internal int GetInvocationLimit(long internalTimerId) => 0;
        internal int GetInvocationCount(long internalTimerId) => 0;
        
        internal InternalTimer GetInternalTimer(long internalId) => null;
        internal InternalTimerBuilder GetInternalTimerBuilder(long internalId) 
            => timerBuilders.GetValueOrDefault(internalId);
        internal Timer RunTimer(InternalTimerBuilder builder)
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(TimerManager));
            timerBuilders.Remove(builder.InternalId);
            return new Timer();
        }

        private void CreateNewInternalTimerBuilder(long internalId)
        {
            var builder = Pooling.PopInternalTimerBuilder();
            builder.Reset();
            builder.InternalId = internalId;
            timerBuilders.Add(internalId, builder);
        }
        
        private void OnUpdate() => ClearTimerBuilders();

        private void ClearTimerBuilders()
        {
            foreach (var builder in timerBuilders.Values) Pooling.ReleaseInternalTimerBuilder(builder);
            timerBuilders.Clear();
        }
        private void OnFixedUpdate() { }
        private void OnLateUpdate() { }
    }
}
