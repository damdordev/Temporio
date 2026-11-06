using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
// ReSharper disable AccessToModifiedClosure

namespace Damdor.Temporio.Tests
{
    public class TimerBuilderTest
    {
        public enum Invalidation
        {
            Run,
            ManagerDisposed,
            NextUpdate
        }

        public enum BuilderMethod
        {
            When,
            After,
            Every,
            InvocationLimit,
            MissingPeriodBehaviour,
            Action,
            ActionWithContext,
            ActionWithParameter,
            ActionWithContextAndParameter,
            Timing,
            CancelOnDestroy,
            CancelIf,
            CancelIfWithParameter,
            CancellationToken,
            DebugName,
            Run
        }

        private TimerManager timerManager;
        private ManualTimeProvider timeProvider;
        private ManualUpdateProvider updateProvider;
        private readonly List<GameObject> owners = new();

        [SetUp]
        public void SetUp()
        {
            timeProvider = new ManualTimeProvider(new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc));
            updateProvider = new ManualUpdateProvider();
            timerManager = new TimerManager(timeProvider, updateProvider);
            timerManager.Run();
        }

        [TearDown]
        public void TearDown()
        {
            timerManager?.Dispose();
            foreach (var owner in owners)
            {
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            }

            owners.Clear();
        }

        [Test]
        public void Schedule_CreatesValidBuilderWithCorrespondingInternalId()
        {
            var builder = timerManager.Schedule();

            Assert.That(builder.IsValid, Is.True);
            var internalBuilder = GetInternalBuilder(builder);
            Assert.That(internalBuilder.Id, Is.EqualTo(builder.id));
            Assert.That(GetInternalBuilder(builder), Is.SameAs(internalBuilder));
            Assert.That(internalBuilder.DebugName, Is.EqualTo(null));
            Assert.That(internalBuilder.Guards.Count, Is.EqualTo(0));
            Assert.That(internalBuilder.Invocations.Count, Is.EqualTo(0));
            Assert.That(internalBuilder.IsRepeating, Is.EqualTo(false));
            Assert.That(internalBuilder.RepeatInterval, Is.EqualTo(TimeSpan.Zero));
            Assert.That(internalBuilder.MissingPeriodBehaviour, Is.EqualTo(MissingPeriodBehaviour.RunOnce));
            Assert.That(internalBuilder.Timing, Is.EqualTo(PlayerLoopTiming.Update));
        }

        [Test]
        public void Schedule_CreatesIndependentBuilders()
        {
            var first = timerManager.Schedule();
            var second = timerManager.Schedule();

            first.DebugName("first");
            second.DebugName("second");

            Assert.That(GetInternalBuilder(first).Id, Is.Not.EqualTo(second.id));
            Assert.That(GetInternalBuilder(first), Is.Not.SameAs(GetInternalBuilder(second)));
            Assert.That(GetInternalBuilder(first).DebugName, Is.EqualTo("first"));
            Assert.That(GetInternalBuilder(second).DebugName, Is.EqualTo("second"));
        }

        [TestCase(-5)]
        [TestCase(0)]
        [TestCase(5)]
        public void When_SetsInternalStartTime(int seconds)
        {
            var builder = timerManager.Schedule();
            var expected = timeProvider.Now.AddSeconds(seconds);

            var result = builder.When(expected);

            Assert.That(GetInternalBuilder(builder).StartTime, Is.EqualTo(expected));
            Assert.That(GetInternalBuilder(result).Id, Is.EqualTo(builder.id));
        }

        [TestCase(-5)]
        [TestCase(0)]
        [TestCase(5)]
        public void After_SetsInternalStartTimeRelativeToManualTime(int seconds)
        {
            var builder = timerManager.Schedule();
            var delay = TimeSpan.FromSeconds(seconds);

            builder.After(delay);

            Assert.That(GetInternalBuilder(builder).StartTime, Is.EqualTo(timeProvider.Now + delay));
        }

        [Test]
        public void Every_SetsInternalRepeatingFlagAndInterval()
        {
            var builder = timerManager.Schedule();
            var period = TimeSpan.FromSeconds(7);

            builder.Every(period);

            Assert.That(GetInternalBuilder(builder).IsRepeating, Is.True);
            Assert.That(GetInternalBuilder(builder).RepeatInterval, Is.EqualTo(period));
        }

        [Test]
        public void InvocationLimit_SetsInternalInvocationLimit()
        {
            var builder = timerManager.Schedule();

            builder.InvocationLimit(3);

            Assert.That(GetInternalBuilder(builder).InvocationLimit, Is.EqualTo(3));
        }

        [TestCase(MissingPeriodBehaviour.RunOnce)]
        [TestCase(MissingPeriodBehaviour.RunAll)]
        public void MissingPeriodBehaviour_SetsInternalBehaviour(MissingPeriodBehaviour behaviour)
        {
            var builder = timerManager.Schedule();

            builder.MissingPeriodBehaviour(behaviour);

            Assert.That(GetInternalBuilder(builder).MissingPeriodBehaviour, Is.EqualTo(behaviour));
        }

        [TestCase(PlayerLoopTiming.Update)]
        [TestCase(PlayerLoopTiming.FixedUpdate)]
        [TestCase(PlayerLoopTiming.LateUpdate)]
        public void Timing_SetsInternalTiming(PlayerLoopTiming timing)
        {
            var builder = timerManager.Schedule();

            builder.Timing(timing);

            Assert.That(GetInternalBuilder(builder).Timing, Is.EqualTo(timing));
        }

        [Test]
        public void DebugName_SetsInternalDebugName()
        {
            var builder = timerManager.Schedule();

            builder.DebugName("test timer");

            Assert.That(GetInternalBuilder(builder).DebugName, Is.EqualTo("test timer"));
        }

        [Test]
        public void Action_AllOverloadsAppendDelegatesAndParametersInOrder()
        {
            var builder = timerManager.Schedule();
            TimerAction action = () => { };
            TimerActionWithContext contextual = _ => { };
            TimerActionWithParameter<int> parametrized = _ => { };
            TimerActionWithContextAndParameter<string> contextualParametrized = (_, _) => { };

            builder.Action(action).Action(contextual).Action(42, parametrized)
                .Action("payload", contextualParametrized).Action(action);

            var invocations = GetInternalBuilder(builder).Invocations;
            Assert.That(invocations, Has.Count.EqualTo(5));
            Assert.That(((TimerInvocationWithoutParameter)invocations[0]).Action, Is.SameAs(action));
            Assert.That(((TimerInvocationWithoutParameter)invocations[1]).ActionWithContext, Is.SameAs(contextual));
            var withParameter = (TimerInvocationWithParameter<int>)invocations[2];
            Assert.That(withParameter.Action, Is.SameAs(parametrized));
            Assert.That(withParameter.Parameter, Is.EqualTo(42));
            var withContextAndParameter = (TimerInvocationWithParameter<string>)invocations[3];
            Assert.That(withContextAndParameter.ActionWithContext, Is.SameAs(contextualParametrized));
            Assert.That(withContextAndParameter.Parameter, Is.EqualTo("payload"));
            Assert.That(((TimerInvocationWithoutParameter)invocations[4]).Action, Is.SameAs(action));
        }

        [Test]
        public void Action_NullDelegatesDoNotAppendInvocations()
        {
            var builder = timerManager.Schedule();
            builder.Action(() => { });

            builder.Action((TimerAction)null).Action((TimerActionWithContext)null)
                .Action(42, (TimerActionWithParameter<int>)null)
                .Action(42, (TimerActionWithContextAndParameter<int>)null);

            Assert.That(GetInternalBuilder(builder).Invocations, Has.Count.EqualTo(1));
        }

        [Test]
        public void CancelIf_AppendsMultipleConditionsAndPreservesParameters()
        {
            var builder = timerManager.Schedule();
            var cancel = false;
            var receivedParameter = 0;
            builder.CancelIf(() => cancel).CancelIf(42, value =>
            {
                receivedParameter = value;
                return cancel;
            });

            var guards = GetInternalBuilder(builder).Guards;
            Assert.That(guards, Has.Count.EqualTo(2));
            Assert.That(guards[0], Is.TypeOf<ConditionTimerGuard>());
            Assert.That(guards[1], Is.TypeOf<ParametrizeConditionTimerGuard<int>>());
            Assert.That(guards[0].Check(), Is.True);
            Assert.That(guards[1].Check(), Is.True);
            Assert.That(receivedParameter, Is.EqualTo(42));

            cancel = true;

            Assert.That(guards[0].Check(), Is.False);
            Assert.That(guards[1].Check(), Is.False);
        }

        [Test]
        public void CancelIf_NullConditionsDoNotAppendGuards()
        {
            var builder = timerManager.Schedule();
            builder.CancelIf(() => false);

            builder.CancelIf(null).CancelIf(42, null);

            Assert.That(GetInternalBuilder(builder).Guards, Has.Count.EqualTo(1));
        }

        [Test]
        public void CancelOnDestroy_AppendsMultipleOwnersAlongsideConditionGuards()
        {
            var builder = timerManager.Schedule();
            var firstOwner = CreateOwner();
            var secondOwner = CreateOwner();

            builder.CancelOnDestroy(firstOwner).CancelIf(() => false).CancelOnDestroy(secondOwner);

            var guards = GetInternalBuilder(builder).Guards;
            Assert.That(guards, Has.Count.EqualTo(3));
            Assert.That(guards[0], Is.TypeOf<GameObjectTimerGuard>());
            Assert.That(guards[2], Is.TypeOf<GameObjectTimerGuard>());
            Assert.That(guards[0].Check(), Is.True);
            Assert.That(guards[2].Check(), Is.True);

            UnityEngine.Object.DestroyImmediate(firstOwner);

            Assert.That(guards[0].Check(), Is.False);
            Assert.That(guards[1].Check(), Is.True);
            Assert.That(guards[2].Check(), Is.True);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void InitialTime_WhenAlreadyConfigured_ThrowsInvalidOperationException(bool firstUsesAfter,
            bool secondUsesAfter)
        {
            var builder = timerManager.Schedule();
            var expected = timeProvider.Now.AddSeconds(5);
            if (firstUsesAfter) builder.After(TimeSpan.FromSeconds(5));
            else builder.When(expected);

            Assert.Throws<InvalidOperationException>(() =>
            {
                if (secondUsesAfter) builder.After(TimeSpan.FromSeconds(10));
                else builder.When(timeProvider.Now.AddSeconds(10));
            });
            Assert.That(builder.IsValid, Is.True);
            Assert.That(GetInternalBuilder(builder).StartTime, Is.EqualTo(expected));
        }

        [Test]
        public void IsValid_ImmediatelyAfterSchedule_IsTrue()
        {
            Assert.That(timerManager.Schedule().IsValid, Is.True);
        }

        [Test]
        public void IsValid_DefaultBuilder_IsFalse()
        {
            Assert.That(default(TimerBuilder).IsValid, Is.False);
        }

        [Test]
        public void IsValid_AfterConfiguration_IsTrueForOriginalAndReturnedBuilder()
        {
            var builder = timerManager.Schedule();

            var configured = builder.After(TimeSpan.FromSeconds(5)).Every(TimeSpan.FromSeconds(2))
                .InvocationLimit(3).MissingPeriodBehaviour(MissingPeriodBehaviour.RunAll)
                .Action(() => { }).Timing(PlayerLoopTiming.LateUpdate)
                .CancelIf(() => false).DebugName("configured");

            Assert.That(builder.IsValid, Is.True);
            Assert.That(configured.IsValid, Is.True);
            Assert.That(GetInternalBuilder(configured), Is.SameAs(GetInternalBuilder(builder)));
        }

        [Test]
        public void IsValid_AfterRun_IsFalseForAllCopiesWhileOtherBuildersRemainValid()
        {
            var builder = timerManager.Schedule();
            var copy = builder;
            var other = timerManager.Schedule();
            Assert.That(builder.IsValid, Is.True);
            Assert.That(other.IsValid, Is.True);

            builder.Run();

            Assert.That(builder.IsValid, Is.False);
            Assert.That(copy.IsValid, Is.False);
            Assert.That(other.IsValid, Is.True);
            Assert.That(timerManager.Schedule().IsValid, Is.True);
            Assert.That(builder.IsValid, Is.False, "Pool reuse must not revive a builder that already ran.");
        }

        [Test]
        public void IsValid_AfterManagerDisposed_IsFalseForAllBuilders()
        {
            var first = timerManager.Schedule();
            var second = timerManager.Schedule();
            Assert.That(first.IsValid, Is.True);
            Assert.That(second.IsValid, Is.True);

            DisposeManager();

            Assert.That(first.IsValid, Is.False);
            Assert.That(second.IsValid, Is.False);
        }

        [Test]
        public void IsValid_AfterUpdate_IsFalseAndNewBuilderIsValid()
        {
            var builder = timerManager.Schedule();
            Assert.That(builder.IsValid, Is.True);

            updateProvider.Tick(PlayerLoopTiming.Update);

            Assert.That(builder.IsValid, Is.False);
            Assert.That(timerManager.Schedule().IsValid, Is.True);
            Assert.That(builder.IsValid, Is.False, "A new builder must not revive an expired handle.");
        }

        [Test]
        public void Run_AfterUpdate_ThrowsTimerBuilderInvalidException()
        {
            var builder = timerManager.Schedule();

            updateProvider.Tick(PlayerLoopTiming.Update);

            Assert.Throws<TimerBuilderInvalidException>(() => builder.Run());
        }

        [Test]
        public void Method_OnInvalidBuilder_ThrowsDocumentedException(
            [Values] BuilderMethod method, [Values] Invalidation invalidation)
        {
            var builder = timerManager.Schedule();
            switch (invalidation)
            {
                case Invalidation.Run: builder.Run(); break;
                case Invalidation.ManagerDisposed: DisposeManager(); break;
                case Invalidation.NextUpdate: updateProvider.Tick(PlayerLoopTiming.Update); break;
                default: throw new ArgumentOutOfRangeException(nameof(invalidation));
            }

            Assert.Throws(typeof(TimerBuilderInvalidException), () => Invoke(method, builder));
        }

        [Test]
        public void Method_OnDefaultBuilder_ThrowsTimerBuilderInvalidException([Values] BuilderMethod method)
        {
            Assert.Throws<TimerBuilderInvalidException>(() => Invoke(method, default));
        }

        [Test]
        public void Run_CreatesTimerWithConfiguredParameters()
        {
            var start = timeProvider.Now.AddSeconds(10);
            var period = TimeSpan.FromSeconds(2);
            var builder = timerManager.Schedule().When(start).Every(period).InvocationLimit(3)
                .Timing(PlayerLoopTiming.LateUpdate).MissingPeriodBehaviour(MissingPeriodBehaviour.RunAll)
                .DebugName("configured timer").Action(() => { }).CancelIf(() => false)
                .CancellationToken(new CancellationToken(true));
            // Snapshot the contents: Run may reset and release the internal builder to its pool.
            var invocations = GetInternalBuilder(builder).Invocations.ToArray();
            var guards = GetInternalBuilder(builder).Guards.ToArray();

            var timer = builder.Run();

            Assert.That(builder.IsValid, Is.False);
            Assert.That(timer.IsAlive, Is.True);
            Assert.That(timer.IsRepeating, Is.True);
            Assert.That(timer.RepeatInterval, Is.EqualTo(period));
            Assert.That(timer.NextInvocationTime, Is.EqualTo(start));
            Assert.That(timer.TimeToNextInvocation, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(timer.InvocationLimit, Is.EqualTo(3));
            Assert.That(timer.InvocationCount, Is.Zero);
            var internalTimer = timerManager.GetInternalTimer(timer.id);
            Assert.That(internalTimer, Is.Not.Null);
            Assert.That(internalTimer.StartTime, Is.EqualTo(start));
            Assert.That(internalTimer.RepeatInterval, Is.EqualTo(period));
            Assert.That(internalTimer.Timing, Is.EqualTo(PlayerLoopTiming.LateUpdate));
            Assert.That(internalTimer.MissingPeriodBehaviour, Is.EqualTo(MissingPeriodBehaviour.RunAll));
            Assert.That(internalTimer.Invocations, Is.EqualTo(invocations));
            Assert.That(internalTimer.Guards, Is.EqualTo(guards));
            Assert.That(internalTimer.DebugName, Is.EqualTo("configured timer"));
        }
        
        private void Invoke(BuilderMethod method, TimerBuilder builder)
        {
            switch (method)
            {
                case BuilderMethod.When: builder.When(timeProvider.Now); break;
                case BuilderMethod.After: builder.After(TimeSpan.FromSeconds(1)); break;
                case BuilderMethod.Every: builder.Every(TimeSpan.FromSeconds(1)); break;
                case BuilderMethod.InvocationLimit: builder.InvocationLimit(3); break;
                case BuilderMethod.MissingPeriodBehaviour:
                    builder.MissingPeriodBehaviour(MissingPeriodBehaviour.RunAll); break;
                case BuilderMethod.Action: builder.Action(() => { }); break;
                case BuilderMethod.ActionWithContext: builder.Action(_ => { }); break;
                case BuilderMethod.ActionWithParameter: builder.Action(42, _ => { }); break;
                case BuilderMethod.ActionWithContextAndParameter:
                    builder.Action(42, (_, _) => { }); break;
                case BuilderMethod.Timing: builder.Timing(PlayerLoopTiming.LateUpdate); break;
                case BuilderMethod.CancelOnDestroy: builder.CancelOnDestroy(CreateOwner()); break;
                case BuilderMethod.CancelIf: builder.CancelIf(() => false); break;
                case BuilderMethod.CancelIfWithParameter: builder.CancelIf(42, _ => false); break;
                case BuilderMethod.CancellationToken: builder.CancellationToken(default); break;
                case BuilderMethod.DebugName: builder.DebugName("invalid"); break;
                case BuilderMethod.Run: builder.Run(); break;
                default: throw new ArgumentOutOfRangeException(nameof(method));
            }
        }

        private GameObject CreateOwner()
        {
            var owner = new GameObject("TimerBuilderTest owner");
            owners.Add(owner);
            return owner;
        }

        private void DisposeManager()
        {
            timerManager.Dispose();
            timerManager = null;
        }

        private InternalTimerBuilder GetInternalBuilder(TimerBuilder builder)
        {
            var internalBuilder = timerManager.GetInternalTimerBuilder(builder.id);
            Assert.That(internalBuilder, Is.Not.Null, "Schedule must register the corresponding internal builder.");
            return internalBuilder;
        }
        
    }
}
