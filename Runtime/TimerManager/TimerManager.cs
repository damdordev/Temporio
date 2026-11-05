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
        private readonly Dictionary<long, InternalTimer> timers = new();
        private long lastTimerBuilderInternalId;
        private long lastTimerInternalId;
        private bool isDisposed;
        private bool isRunning;
        private bool isUpdating;
        private InternalTimer executingTimer;
        private TimerExceptionHandler errorHandler;
        private readonly List<long> updateTimers = new();
        
        /// <summary>
        /// Creates a new instance of <see cref="TimerManager"/>. 
        /// </summary>
        /// <param name="timeProvider">Provider for current time.</param>
        /// <param name="updateProvider">Provider for update events. If null, <see cref="MonoUpdateProvider"/> will be used.</param>
        /// <exception cref="ArgumentNullException">Thrown if <see cref="timeProvider"/> parameter is null.</exception>
        public TimerManager(ITimeProvider timeProvider, IUpdateProvider updateProvider = null)
        {
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.updateProvider = updateProvider ?? new MonoUpdateProvider();
            weakReference = new WeakReference<TimerManager>(this);
            Pooling = new TemporioPooling();
        }

        /// <summary>
        /// Starts TimerManager. Since then you can use the timer manager to schedule and manage timers.
        /// <remarks>TimerManager cannot be run twice</remarks>
        /// </summary>
        public void Run()
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(TimerManager));
            if (isRunning) throw new InvalidOperationException("TimerManager is already running.");
            isRunning = true;
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
            foreach (var timer in timers.Values)
            {
                if (timer != executingTimer) ReleaseInternalTimer(timer);
            }

            timers.Clear();
            updateProvider.Unregister(PlayerLoopTiming.Update, OnUpdate);
            updateProvider.Unregister(PlayerLoopTiming.FixedUpdate, OnFixedUpdate);
            updateProvider.Unregister(PlayerLoopTiming.LateUpdate, OnLateUpdate);
            
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
            this.errorHandler = errorHandler;
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
        public bool CancelTimerOnException { get; set; }
        
        internal InternalTimer GetInternalTimer(long internalId)
            => timers.GetValueOrDefault(internalId);
        internal InternalTimerBuilder GetInternalTimerBuilder(long internalId) 
            => timerBuilders.GetValueOrDefault(internalId);
        internal Timer RunTimer(InternalTimerBuilder builder)
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(TimerManager));
            if (builder.IsRepeating && builder.RepeatInterval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(builder.RepeatInterval), "Repeat interval must be positive.");
            timerBuilders.Remove(builder.InternalId);
            var internalId = checked(++lastTimerInternalId);
            CreateNewInternalTimer(internalId, builder);
            ReleaseInternalTimerBuilder(builder);
            return new Timer(weakReference, internalId);
        }

        internal bool CancelTimer(long internalId)
        {
            if (!timers.Remove(internalId, out var timer)) return false;
            if (timer != executingTimer) ReleaseInternalTimer(timer);
            return true;
        }

        private void CreateNewInternalTimerBuilder(long internalId)
        {
            var builder = Pooling.PopInternalTimerBuilder();
            builder.Reset();
            builder.InternalId = internalId;
            timerBuilders.Add(internalId, builder);
        }
        
        private void CreateNewInternalTimer(long internalId, InternalTimerBuilder builder)
        {
            var timer = Pooling.PopInternalTimer();
            timer.Reset();
            timer.InternalId = internalId;
            timer.StartTime = builder.HasStartTime ? builder.StartTime : Now;
            timer.NextInvocationTime = timer.StartTime;
            timer.RepeatInterval = builder.RepeatInterval;
            timer.IsRepeating = builder.IsRepeating;
            timer.Invocations.AddRange(builder.Invocations);
            timer.Guards.AddRange(builder.Guards);
            timer.DebugName = builder.DebugName;
            timer.InvocationLimit = builder.InvocationLimit;
            timer.MissingPeriodBehaviour = builder.MissingPeriodBehaviour;
            timer.Timing = builder.Timing;
            builder.Invocations.Clear();
            builder.Guards.Clear();

            timers[internalId] = timer;
        }
        
        private void OnUpdate()
        {
            ClearTimerBuilders();
            ProcessTimers(PlayerLoopTiming.Update);
        }

        private void ClearTimerBuilders()
        {
            foreach (var builder in timerBuilders.Values) ReleaseInternalTimerBuilder(builder);
            timerBuilders.Clear();
        }
        private void OnFixedUpdate() => ProcessTimers(PlayerLoopTiming.FixedUpdate);
        private void OnLateUpdate() => ProcessTimers(PlayerLoopTiming.LateUpdate);

        private void ProcessTimers(PlayerLoopTiming timing)
        {
            if (isDisposed || isUpdating) return;
            isUpdating = true;
            try
            {
                var now = Now;
                updateTimers.AddRange(timers.Keys);
                foreach (var internalId in updateTimers)
                {
                    if (isDisposed) break;
                    if (!timers.TryGetValue(internalId, out var timer) || timer.Timing != timing) continue;
                    executingTimer = timer;
                    try
                    {
                        ProcessTimer(timer, now);
                    }
                    finally
                    {
                        executingTimer = null;
                        if (!timers.ContainsKey(internalId)) ReleaseInternalTimer(timer);
                    }
                }
            }
            finally
            {
                updateTimers.Clear();
                isUpdating = false;
            }
        }

        private void ProcessTimer(InternalTimer timer, DateTime now)
        {
            foreach (var guard in timer.Guards)
            {
                try
                {
                    if (guard.Check() && timers.ContainsKey(timer.InternalId)) continue;
                }
                catch (Exception exception)
                {
                    ReportException(timer, exception);
                }
                CancelTimer(timer.InternalId);
                return;
            }

            while (timers.ContainsKey(timer.InternalId) && timer.NextInvocationTime <= now)
            {
                var context = new TimerInvocationContext(timer.InvocationCount, now, timer.NextInvocationTime);
                var cancelOnException = false;
                foreach (var invocation in timer.Invocations)
                {
                    try
                    {
                        invocation.Fire(context);
                    }
                    catch (Exception exception)
                    {
                        cancelOnException |= CancelTimerOnException;
                        ReportException(timer, exception);
                    }
                }
                timer.InvocationCount++;

                if (!timer.IsRepeating || cancelOnException ||
                    (timer.InvocationLimit > 0 && timer.InvocationCount >= timer.InvocationLimit))
                    CancelTimer(timer.InternalId);
                if (!timers.ContainsKey(timer.InternalId)) return;

                var periods = timer.MissingPeriodBehaviour == MissingPeriodBehaviour.RunAll
                    ? 1
                    : (now - timer.NextInvocationTime).Ticks / timer.RepeatInterval.Ticks + 1;
                if (periods > (DateTime.MaxValue.Ticks - timer.NextInvocationTime.Ticks) / timer.RepeatInterval.Ticks)
                {
                    CancelTimer(timer.InternalId);
                    return;
                }
                timer.NextInvocationTime = timer.NextInvocationTime.AddTicks(periods * timer.RepeatInterval.Ticks);
            }
        }

        private void ReportException(InternalTimer timer, Exception exception)
        {
            if (LogErrors) UnityEngine.Debug.LogException(exception);
            try
            {
                errorHandler?.Invoke(timer.DebugName, exception);
            }
            catch (Exception handlerException)
            {
                if (LogErrors) UnityEngine.Debug.LogException(handlerException);
            }
        }
        
        private void ReleaseInternalTimer(InternalTimer internalTimer)
        {
            foreach (var invocation in internalTimer.Invocations) Pooling.ReleaseTimerInvocation(invocation);
            foreach (var guard in internalTimer.Guards) Pooling.ReleaseGuard(guard);

            Pooling.ReleaseInternalTimer(internalTimer);
        }
        
        private void ReleaseInternalTimerBuilder(InternalTimerBuilder builder)
        {
            foreach (var invocation in builder.Invocations) Pooling.ReleaseTimerInvocation(invocation);
            builder.Invocations.Clear();
            foreach (var guard in builder.Guards) Pooling.ReleaseGuard(guard);
            builder.Guards.Clear();

            Pooling.ReleaseInternalTimerBuilder(builder);
        }
        
        
    }
}
