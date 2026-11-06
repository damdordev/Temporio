using System;
using UnityEngine;

namespace Damdor.Temporio
{
    /// <summary>
    /// Provides a builder for configuring and scheduling timers. Instances of this class
    /// are returned by the <see cref="TimerManager.Schedule"/> method and allow for detailed
    /// customization of timer behavior before starting it with the <see cref="Run"/> method.
    /// <para>
    /// Builder is short-lived and can be used only in the frame in which it was created.
    /// </para>
    /// </summary>
    public struct TimerBuilder : IEquatable<TimerBuilder>
    {
        private WeakReference<TimerManager> timerManager;
        internal long id;

        /// <summary>
        /// Gets a value indicating whether this builder is still valid and can be used to configure and run the timer.
        /// Builder can be invalidated in following cases:
        /// <list type="bullet">
        /// <item><description>When <see cref="Run"/> method is called.</description></item>
        /// <item><description>When TimerManager is disposed</description></item>
        /// <item><description>When TimerBuilder is used in a different frame then is was created</description></item>
        /// </list>
        /// </summary>
        public bool IsValid => GetInternalTimerBuilder() != null;

        internal void Setup(WeakReference<TimerManager> timerManager, long id)
        {
            this.timerManager = timerManager;
            this.id = id;
        }

        /// <summary>
        /// Specifies the exact time at which the timer will be invoked.
        /// </summary>
        /// <param name="invocationTime">
        /// The <see cref="DateTime"/> representing the exact time the timer should be triggered.
        /// <remarks>If the timer is periodic, this specifies the initial invocation time.</remarks>
        /// <remarks>If the value is in the pase, the timer will be invoked in the next corresponding update phase</remarks>
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        /// <exception cref="InvalidOperationException">Thrown initial time was already setup by this or <see cref="After(TimeSpan)"/> method.</exception>
        public TimerBuilder When(DateTime invocationTime)
        {
            var builder = GetInternalTimerBuilderOrThrow();
            if (builder.HasStartTime) throw new InvalidOperationException("Initial time is already configured.");
            builder.StartTime = invocationTime;
            builder.HasStartTime = true;
            return this;
        }

        /// <summary>
        /// Specifies the time from now at which the timer will be invoked.
        /// </summary>
        /// <param name="timeFromNow">
        /// The <see cref="TimeSpan"/> representing the time from now at which the timer should be triggered.
        /// <remarks>If the timer is periodic, this specifies the initial invocation time.</remarks>
        /// <remarks>If the value is in the pase, the timer will be invoked in the next corresponding update phase</remarks>
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        /// <exception cref="InvalidOperationException">Thrown initial time was already setup by this or <see cref="When(DateTime)"/> method.</exception>

        public TimerBuilder After(TimeSpan timeFromNow)
        {
            GetInternalTimerBuilderOrThrow();
            timerManager.TryGetTarget(out var manager);
            return When(manager!.Now + timeFromNow);
        }

        /// <summary>
        /// Sets a periodic invocation interval for the timer.
        /// Mind that scheduled invocation times are not calculated based only on start time and period - so missing periods are not automatically compensated.
        /// </summary>
        /// <param name="period">
        /// A <see cref="TimeSpan"/> representing the interval at which the timer will be triggered repeatedly.
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        public TimerBuilder Every(TimeSpan period)
        {
            var builder = GetInternalTimerBuilderOrThrow();
            builder.IsRepeating = true;
            builder.RepeatInterval = period;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of times the timer may be invoked before it stops. The number includes the initial invocation.
        /// </summary>
        /// <param name="limit">
        /// The maximum number of invocations allowed for the timer. 0 means no limit.
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        public TimerBuilder InvocationLimit(int limit)
        {
            GetInternalTimerBuilderOrThrow().InvocationLimit = limit;
            return this;
        }

        /// <summary>
        /// Defines the behavior of the timer when one or more periods have been missed
        /// due to delays or other interruptions. The default value is <see cref="MissingPeriodBehaviour.RunOnce"/>.
        /// Mind that scheduled invocation times are not calculated based only on start time and period - so missing periods are not automatically compensated.
        /// </summary>
        /// <param name="behaviour">
        /// The <see cref="MissingPeriodBehaviour"/> enum value specifying the desired handling of missed periods:
        /// <remarks><see cref="MissingPeriodBehaviour.RunOnce"/> executes the timer only for the current period.</remarks>
        /// <remarks><see cref="MissingPeriodBehaviour.RunAll"/> executes the timer for all missed periods.</remarks>
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        public TimerBuilder MissingPeriodBehaviour(MissingPeriodBehaviour behaviour)
        {
            GetInternalTimerBuilderOrThrow().MissingPeriodBehaviour = behaviour;
            return this;
        }

        /// <summary>
        /// Specifies the action to execute when the timer is triggered.
        /// Timer can have multiple actions associated with it, which will be executed sequentially.
        /// </summary>
        /// <param name="action">
        /// The <see cref="TimerAction"/> delegate representing the method to invoke when the timer fires.
        /// <remarks>When the action is null, this method does nothing</remarks>
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        public TimerBuilder Action(TimerAction action)
        {
            var builder = GetInternalTimerBuilderOrThrow();
            if (action == null) return this;
            timerManager.TryGetTarget(out var manager);
            var invocation = manager!.Pooling.PopTimerInvocationWithoutParameter();
            invocation.Action = action;
            builder.Invocations.Add(invocation);
            return this;
        }

        /// <summary>
        /// Defines the action to be performed when the timer is triggered.
        /// Timer can have multiple actions associated with it, which will be executed sequentially.
        /// </summary>
        /// <param name="action">
        /// A delegate of type <see cref="TimerActionWithContext"/>, representing the action to execute.
        /// The parameter provides access to the <see cref="TimerInvocationContext"/> containing details about the invocation.
        /// <remarks>When the action is null, this method does nothing</remarks>
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        public TimerBuilder Action(TimerActionWithContext action)
        {
            var builder = GetInternalTimerBuilderOrThrow();
            if (action == null) return this;
            timerManager.TryGetTarget(out var manager);
            var invocation = manager!.Pooling.PopTimerInvocationWithoutParameter();
            invocation.ActionWithContext = action;
            builder.Invocations.Add(invocation);
            return this;
        }

        /// <summary>
        /// Specifies the action to execute when the timer is triggered.
        /// Timer can have multiple actions associated with it, which will be executed sequentially.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the parameter that will be passed to the action.
        /// </typeparam>
        /// <param name="parameter">
        /// The parameter of type <typeparamref name="T"/> to pass to the action when the timer is triggered.
        /// </param>
        /// <param name="action">
        /// The action to execute, which accepts a parameter of type <typeparamref name="T"/>.
        /// <remarks>When the action is null, this method does nothing</remarks>
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        public TimerBuilder Action<T>(T parameter, TimerActionWithParameter<T> action)
        {
            var builder = GetInternalTimerBuilderOrThrow();
            if (action == null) return this;
            timerManager.TryGetTarget(out var manager);
            var invocation = manager!.Pooling.PopTimerInvocationWithParameter<T>();
            invocation.Action = action;
            invocation.Parameter = parameter;
            builder.Invocations.Add(invocation);
            return this;
        }

        /// <summary>
        /// Specifies the action to execute when the timer is triggered, with access to context information and a parameter.
        /// Timer can have multiple actions associated with it, which will be executed sequentially.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the parameter passed to the action when the timer is invoked.
        /// </typeparam>
        /// <param name="parameter">
        /// The parameter of type <typeparamref name="T"/> to be passed to the action when the timer is invoked.
        /// </param>
        /// <param name="action">
        /// The <see cref="TimerActionWithContextAndParameter{T}"/> delegate that represents the action to be performed.
        /// It takes a <see cref="TimerInvocationContext"/> and the specified parameter of type <typeparamref name="T"/>.
        /// <remarks>When the action is null, this method does nothing</remarks>
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        public TimerBuilder Action<T>(T parameter, TimerActionWithContextAndParameter<T> action)
        {
            var builder = GetInternalTimerBuilderOrThrow();
            if (action == null) return this;
            timerManager.TryGetTarget(out var manager);
            var invocation = manager!.Pooling.PopTimerInvocationWithParameter<T>();
            invocation.ActionWithContext = action;
            invocation.Parameter = parameter;
            builder.Invocations.Add(invocation);
            return this;
        }

        /// <summary>
        /// Specifies the timing within the Unity player loop at which the timer will be executed.
        /// The default is <see cref="PlayerLoopTiming.Update"/>.
        /// </summary>
        /// <param name="timing">
        /// The <see cref="PlayerLoopTiming"/> representing the chosen phase of the Unity player loop
        /// during which the timer action should be triggered.
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        public TimerBuilder Timing(PlayerLoopTiming timing)
        {
            GetInternalTimerBuilderOrThrow().Timing = timing;
            return this;
        }

        /// <summary>
        /// Ensures that the timer is associated with a specific owner and will only remain active
        /// as long as the owner exists in the scene. The timer will be canceled if the owner is destroyed.
        /// Timer can have multiple guards associated with it. If any of the guards are triggered, the timer will be canceled.
        /// </summary>
        /// <param name="owner">
        /// The <see cref="GameObject"/> that acts as the owner of the timer. The timer is bound to the lifecycle of this GameObject,
        /// and will automatically stop if the GameObject is destroyed.
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        public TimerBuilder CancelOnDestroy(GameObject owner)
        {
            var builder = GetInternalTimerBuilderOrThrow();
            timerManager.TryGetTarget(out var manager);
            var guard = manager!.Pooling.PopGameObjectTimerGuard();
            guard.Setup(owner);
            builder.Guards.Add(guard);
            return this;
        }

        /// <summary>
        /// Adds a guard condition to the timer that determines whether the timer should execute.
        /// Timer can have multiple guards associated with it. If any of the guards are triggered, the timer will be canceled.
        /// </summary>
        /// <param name="condition">
        /// A delegate of type <see cref="TimerGuardCondition"/> that represents the condition to evaluate.
        /// The timer will only execute if this condition evaluates to <see langword="false"/> when checked.
        /// <remarks>If the condition is null, this method does nothing</remarks>
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        public TimerBuilder CancelIf(TimerGuardCondition condition)
        {
            var builder = GetInternalTimerBuilderOrThrow();
            if (condition == null) return this;
            timerManager.TryGetTarget(out var manager);
            var guard = manager!.Pooling.PopConditionTimerGuard();
            guard.Setup(condition);
            builder.Guards.Add(guard);
            return this;
        }

        /// <summary>
        /// Defines a guard condition for the timer, allowing the timer's execution
        /// to be dependent on a parametrized condition that evaluates to true or false.
        /// Timer can have multiple guards associated with it. If any of the guards are triggered, the timer will be canceled.
        /// </summary>
        /// <param name="parameter">
        /// A value of type <typeparamref name="T"/> that will be passed to the guard condition.
        /// </param>
        /// <param name="condition">
        /// A delegate of type <see cref="ParametrizeTimerGuardCondition{T}"/> that evaluates the guard condition.
        /// The timer will only execute if the condition returns false.
        /// <remarks>If the condition is null, this method does nothing</remarks>
        /// </param>
        /// <typeparam name="T">
        /// The type of the parameter passed into the guard condition.
        /// </typeparam>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        public TimerBuilder CancelIf<T>(T parameter, ParametrizeTimerGuardCondition<T> condition)
        {
            var builder = GetInternalTimerBuilderOrThrow();
            if (condition == null) return this;
            timerManager.TryGetTarget(out var manager);
            var guard = manager!.Pooling.PopParametrizeConditionTimerGuard<T>();
            guard.Setup(condition, parameter);
            builder.Guards.Add(guard);
            return this;
        }

        /// <summary>
        /// Assigns a debug name to the timer for identification purposes during development and debugging.
        /// </summary>
        /// <param name="name">
        /// A string representing the debug name of the timer. This is purely for developer reference and does not
        /// affect timer functionality.
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        public TimerBuilder DebugName(string name)
        {
            GetInternalTimerBuilderOrThrow().DebugName = name;
            return this;
        }

        /// <summary>
        /// Assigns the configured timer to the specified timer group.
        /// </summary>
        /// <param name="group">
        /// The <see cref="TimerGroup"/> that will be associated with the timer.
        /// </param>
        /// <returns>
        /// The current instance of <see cref="TimerBuilder"/>, allowing for method chaining.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        /// <exception cref="ArgumentException">Thrown if the provided <paramref name="group"/> is not alive or was created
        /// for a different manager</exception>
        public TimerBuilder Group(TimerGroup group)
        {
            var internalTimerBuilder = GetInternalTimerBuilderOrThrow();
            group.timerManager.TryGetTarget(out var groupManager);
            timerManager.TryGetTarget(out var myManager);
            if(!group.IsAlive) throw new ArgumentException("The provided group is not alive");
            if(groupManager != myManager) throw new ArgumentException("The provided group was created for a different manager");
            internalTimerBuilder.TimerGroup = group.id;
            return this;
        }

        /// <summary>
        /// Finalizes the configuration of the timer and initiates its execution. This method can be used only
        /// once. After calling this method, you cannot use this instance of <see cref="TimerBuilder"/> as it returns
        /// to the internal pool for reuse.
        /// </summary>
        /// <remarks>
        /// Builder is short-lived and can be used only in the frame in which it was created.
        /// </remarks>
        /// <returns>
        /// A <see cref="Timer"/> representing the timer instance that has been launched.
        /// The returned <see cref="Timer"/> can be used to query the timer's status,
        /// manage its lifecycle, and access other runtime characteristics.
        /// </returns>
        /// <exception cref="TimerBuilderInvalidException">Thrown if used on a builder that is not valid</exception>
        public Timer Run()
        {
            var builder = GetInternalTimerBuilderOrThrow();
            timerManager.TryGetTarget(out var manager);
            return manager!.RunTimer(builder);
        }
        
        /// <summary>
        /// Determines whether both handles identify the same builder in the same manager,
        /// regardless of whether it is still active.
        /// </summary>
        public readonly bool Equals(TimerBuilder other) =>
            ReferenceEquals(timerManager, other.timerManager) && id == other.id;

        /// <inheritdoc/>
        public override readonly bool Equals(object obj) => obj is TimerBuilder other && Equals(other);

        /// <inheritdoc/>
        public override readonly int GetHashCode() => HashCode.Combine(timerManager, id);

        /// <summary>
        /// Determines whether two handles are equal.
        /// </summary>
        public static bool operator ==(TimerBuilder left, TimerBuilder right) => left.Equals(right);

        /// <summary>
        /// Determines whether two handles are different.
        /// </summary>
        public static bool operator !=(TimerBuilder left, TimerBuilder right) => !left.Equals(right);

        private InternalTimerBuilder GetInternalTimerBuilder()
        {
            return timerManager != null && timerManager.TryGetTarget(out var manager)
                ? manager.GetInternalTimerBuilder(id)
                : null;
        }

        private InternalTimerBuilder GetInternalTimerBuilderOrThrow()
        {
            return GetInternalTimerBuilder() ?? throw new TimerBuilderInvalidException();
        }
    }
}
