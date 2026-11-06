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
        private readonly Dictionary<long, InternalTimerGroup> timerGroups = new();
        private readonly List<Timer> tmpTimerList = new();
        private long lastTimerBuilderId;
        private long lastTimerId;
        private long lastGroupId = 1;
        private bool isDisposed;
        private bool isRunning;
        private bool isUpdating;
        private InternalTimer executingTimer;
        private TimerExceptionHandler errorHandler;
        private readonly List<long> updateTimers = new();
        private readonly bool updateProviderCreated;
        
        /// <summary>
        /// Creates a new instance of <see cref="TimerManager"/>. 
        /// </summary>
        /// <param name="timeProvider">Provider for current time.</param>
        /// <param name="updateProvider">Provider for update events. If null, <see cref="MonoUpdateProvider"/> will be used.
        /// Passed provider doesn't own the timer manager and will not be disposed when timer manager is disposed.</param>
        /// <exception cref="ArgumentNullException">Thrown if <see cref="timeProvider"/> parameter is null.</exception>
        public TimerManager(ITimeProvider timeProvider, IUpdateProvider updateProvider = null)
        {
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.updateProvider = updateProvider ?? new MonoUpdateProvider();
            updateProviderCreated = updateProvider == null;
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
            // Wait continuations may call back into the manager. Remove timers before completing their waits.
            var timersToRelease = new List<InternalTimer>(timers.Values);
            timers.Clear();
            foreach (var timer in timersToRelease)
            {
                if (timer != executingTimer) ReleaseInternalTimer(timer);
            }
            timerGroups.Clear();

            updateProvider.Unregister(PlayerLoopTiming.Update, OnUpdate);
            updateProvider.Unregister(PlayerLoopTiming.FixedUpdate, OnFixedUpdate);
            updateProvider.Unregister(PlayerLoopTiming.LateUpdate, OnLateUpdate);
            if (updateProviderCreated) updateProvider.Dispose();
            Pooling.Dispose();
        }

        /// <summary>
        /// Creates a new timer using the <see cref="TimerBuilder"/> class.
        /// </summary>
        /// <returns>A <see cref="TimerBuilder"/> instance to configure and run the timer.</returns>
        public TimerBuilder Schedule()
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(TimerManager));
            var internalId = checked(++lastTimerBuilderId);
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
        /// Creates a new named timer group that can be assigned to timers scheduled by this manager.
        /// </summary>
        /// <param name="name">Name assigned to the timer group.</param>
        /// <returns>A <see cref="TimerGroup"/> representing the created timer group.</returns>
        public TimerGroup CreateGroup(string name)
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(TimerManager));
            var id = checked(++lastGroupId);
            var internalGroup = Pooling.PopInternalTimerGroup();
            internalGroup.Reset();
            internalGroup.Id = id;
            internalGroup.Name = name;
            timerGroups.Add(id, internalGroup);
            return new TimerGroup(weakReference, id);
        }

        /// <summary>
        /// Indicates whether a timer is canceled when an exception occurs during its invocation.
        /// Affects only timers that are periodic.
        /// Actions in the current invocation will still be executed.
        /// </summary>
        public bool CancelTimerOnException { get; set; }
        
        internal InternalTimer GetInternalTimer(long id)
            => timers.GetValueOrDefault(id);
        internal InternalTimerBuilder GetInternalTimerBuilder(long id) 
            => timerBuilders.GetValueOrDefault(id);
        internal InternalTimerGroup GetInternalGroupId(long id) 
            => timerGroups.GetValueOrDefault(id);
        
        internal Timer RunTimer(InternalTimerBuilder builder)
        {
            if (isDisposed) 
                throw new ObjectDisposedException(nameof(TimerManager));
            if (builder.IsRepeating && builder.RepeatInterval <= TimeSpan.Zero) 
                throw new ArgumentOutOfRangeException(nameof(builder.RepeatInterval), "Repeat interval must be positive.");
            if (builder.TimerGroup > 0 && !timerGroups.ContainsKey(builder.TimerGroup))
                throw new ArgumentOutOfRangeException(nameof(builder.TimerGroup), "Trying to run timer with disposed group");
            timerBuilders.Remove(builder.Id);
            var internalId = checked(++lastTimerId);
            CreateNewInternalTimer(internalId, builder);
            ReleaseInternalTimerBuilder(builder);
            return new Timer(weakReference, internalId);
        }

        internal bool CancelTimer(long id)
        {
            if (!timers.Remove(id, out var timer)) return false;
            if (timer != executingTimer) ReleaseInternalTimer(timer);
            return true;
        }

        internal void SetTimerPaused(long id, bool paused)
        {
            if (timers.TryGetValue(id, out var timer)) SetTimerPaused(timer, paused, false, Now);
        }

        internal void SetGroupPaused(long id, bool paused)
        {
            if (isDisposed || !timerGroups.TryGetValue(id, out var group) || group.IsPaused == paused) return;
            group.IsPaused = paused;
            var now = Now;
            GetTimersForGroup(id, tmpTimerList);
            foreach (var timer in tmpTimerList)
                SetTimerPaused(timers[timer.id], paused, true, now);
        }

        private void SetTimerPaused(InternalTimer timer, bool paused, bool byGroup, DateTime now)
        {
            var wasPaused = timer.IsPaused;
            if (byGroup) timer.IsPausedByGroup = paused;
            else timer.IsPausedSelf = paused;

            if (!wasPaused && timer.IsPaused) timer.PausedAt = now;
            if (!wasPaused || timer.IsPaused) return;

            var pauseDuration = now - timer.PausedAt;
            // A timer whose adjusted deadline cannot be represented cannot be scheduled again.
            if (pauseDuration.Ticks > DateTime.MaxValue.Ticks - timer.NextInvocationTime.Ticks ||
                pauseDuration.Ticks < -timer.NextInvocationTime.Ticks)
            {
                CancelTimer(timer.Id);
                return;
            }
            timer.NextInvocationTime += pauseDuration;
            timer.PausedAt = default;
        }

        internal void DisposeGroup(long id)
        {
            CancelGroup(id);
            if(!timerGroups.Remove(id, out var internalTimerGroup)) return;
            Pooling.ReleaseInternalTimerGroup(internalTimerGroup);
        }
        
        internal int CancelGroup(long id)
        {
            GetTimersForGroup(id, tmpTimerList);
            var count = tmpTimerList.Count;
            // A wait continuation can cancel another group and reuse tmpTimerList.
            foreach (var timer in tmpTimerList.ToArray()) CancelTimer(timer.id);
            return count;
        }
        
        internal void GetTimersForGroup(long groupId, List<Timer> timers)
        {
            timers.Clear();
            if (!timerGroups.ContainsKey(groupId)) return;
            
            foreach (var internalTimer in this.timers.Values)
            {
                if (internalTimer.GroupId != groupId) continue;
                timers.Add(new Timer(weakReference, internalTimer.Id));
            }
        }

        private void CreateNewInternalTimerBuilder(long id)
        {
            var builder = Pooling.PopInternalTimerBuilder();
            builder.Reset();
            builder.Id = id;
            timerBuilders.Add(id, builder);
        }
        
        private void CreateNewInternalTimer(long internalId, InternalTimerBuilder builder)
        {
            var timer = Pooling.PopInternalTimer();
            timer.Reset();
            timer.Id = internalId;
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
            timer.GroupId = builder.TimerGroup;
            if (timerGroups.TryGetValue(timer.GroupId, out var group) && group.IsPaused)
            {
                timer.IsPausedByGroup = true;
                timer.PausedAt = Now;
            }
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
                    if (guard.Check() && timers.ContainsKey(timer.Id)) continue;
                }
                catch (Exception exception)
                {
                    ReportException(timer, exception);
                }
                CancelTimer(timer.Id);
                return;
            }

            while (timers.ContainsKey(timer.Id) && !timer.IsPaused && timer.NextInvocationTime <= now)
            {
#if DAMDOR_TEMPORIO_UNITASK
                var completion = timer.NextInvocationCompletion;
                timer.NextInvocationCompletion = null;
#endif
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
                    CancelTimer(timer.Id);
                if (timers.ContainsKey(timer.Id))
                {
                    var periods = timer.MissingPeriodBehaviour == MissingPeriodBehaviour.RunAll
                        ? 1
                        : (now - timer.NextInvocationTime).Ticks / timer.RepeatInterval.Ticks + 1;
                    if (periods > (DateTime.MaxValue.Ticks - timer.NextInvocationTime.Ticks) / timer.RepeatInterval.Ticks)
                        CancelTimer(timer.Id);
                    else
                        timer.NextInvocationTime = timer.NextInvocationTime.AddTicks(periods * timer.RepeatInterval.Ticks);
                }
#if DAMDOR_TEMPORIO_UNITASK
                completion?.TrySetResult(true);
#endif
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
#if DAMDOR_TEMPORIO_UNITASK
            var completion = internalTimer.NextInvocationCompletion;
            internalTimer.NextInvocationCompletion = null;
#endif
            foreach (var invocation in internalTimer.Invocations) invocation.ReturnToPool(Pooling);
            foreach (var guard in internalTimer.Guards) guard.ReturnToPool(Pooling);

            Pooling.ReleaseInternalTimer(internalTimer);
#if DAMDOR_TEMPORIO_UNITASK            
            completion?.TrySetResult(false);
#endif
        }
        
        private void ReleaseInternalTimerBuilder(InternalTimerBuilder builder)
        {
            foreach (var invocation in builder.Invocations) invocation.ReturnToPool(Pooling);
            builder.Invocations.Clear();
            foreach (var guard in builder.Guards) guard.ReturnToPool(Pooling);
            builder.Guards.Clear();

            Pooling.ReleaseInternalTimerBuilder(builder);
        }
        
        
    }
}
