using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
// ReSharper disable AccessToModifiedClosure

namespace Damdor.Temporio.Tests
{
    [TestFixture(PlayerLoopTiming.Update)]
    [TestFixture(PlayerLoopTiming.FixedUpdate)]
    [TestFixture(PlayerLoopTiming.LateUpdate)]
    public class TimerTest
    {
        public enum ActionKind
        {
            WithoutParameter,
            WithParameter,
            WithContext,
            WithContextAndParameter
        }

        private readonly PlayerLoopTiming timing;
        private TimerManager timerManager;
        private ManualTimeProvider timeProvider;
        private ManualUpdateProvider updateProvider;

        public TimerTest(PlayerLoopTiming timing)
        {
            this.timing = timing;
        }

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
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancellationToken_CancelsBeforeFirstInvocation(bool alreadyCancelled)
        {
            using var cancellation = new CancellationTokenSource();
            if (alreadyCancelled) cancellation.Cancel();
            var calls = 0;
            var timer = Schedule().After(TimeSpan.FromSeconds(10))
                .CancellationToken(cancellation.Token).Action(() => calls++).Run();

            if (!alreadyCancelled) cancellation.Cancel();
            Assert.That(timer.IsAlive, Is.True);
            TickAt(timeProvider.Now);

            Assert.That(timer.IsAlive, Is.False);
            TickAt(timeProvider.Now.AddSeconds(20));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void CancellationToken_StopsFutureRepetitions()
        {
            using var cancellation = new CancellationTokenSource();
            var calls = 0;
            var timer = Schedule().Every(TimeSpan.FromSeconds(1))
                .CancellationToken(cancellation.Token).Action(() => calls++).Run();

            TickAt(timeProvider.Now);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(timer.IsAlive, Is.True);

            cancellation.Cancel();
            TickAt(timeProvider.Now.AddSeconds(1));
            TickAt(timeProvider.Now.AddSeconds(1));

            Assert.That(timer.IsAlive, Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancellationToken_UncancelledOrDefaultTokenAllowsInvocation(bool useDefaultToken)
        {
            using var cancellation = new CancellationTokenSource();
            var calls = 0;
            var timer = Schedule().CancellationToken(useDefaultToken ? default : cancellation.Token)
                .Action(() => calls++).Run();

            TickAt(timeProvider.Now);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(timer.IsAlive, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancellationToken_AnyTokenCanCancelAlongsideOtherGuards(bool cancelFirstToken)
        {
            using var first = new CancellationTokenSource();
            using var second = new CancellationTokenSource();
            var calls = 0;
            var timer = Schedule().After(TimeSpan.FromSeconds(10)).CancelIf(() => false)
                .CancellationToken(first.Token).CancellationToken(second.Token)
                .Action(() => calls++).Run();

            TickAt(timeProvider.Now);
            Assert.That(timer.IsAlive, Is.True);
            (cancelFirstToken ? first : second).Cancel();
            TickAt(timeProvider.Now);

            Assert.That(timer.IsAlive, Is.False);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void CancellationToken_IsCheckedOnlyInSelectedUpdatePhase()
        {
            using var cancellation = new CancellationTokenSource();
            var timer = Schedule().After(TimeSpan.FromSeconds(10))
                .CancellationToken(cancellation.Token).Run();
            cancellation.Cancel();

            foreach (PlayerLoopTiming phase in Enum.GetValues(typeof(PlayerLoopTiming)))
                if (phase != timing) updateProvider.Tick(phase);

            Assert.That(timer.IsAlive, Is.True);
            TickAt(timeProvider.Now);
            Assert.That(timer.IsAlive, Is.False);
        }

        [Test]
        public void Run_AssociatesEachTimerWithItsOwnInternalTimerAndMatchingId()
        {
            // Discard a builder so builder IDs and timer IDs cannot accidentally coincide.
            timerManager.Schedule();
            var first = Schedule().After(TimeSpan.FromSeconds(5)).Run();
            var second = Schedule().After(TimeSpan.FromSeconds(10)).Run();

            var firstInternal = GetInternalTimer(first);
            var secondInternal = GetInternalTimer(second);
            Assert.That(firstInternal.Id, Is.EqualTo(first.id));
            Assert.That(secondInternal.Id, Is.EqualTo(second.id));
            Assert.That(first.id, Is.Not.EqualTo(second.id));
            Assert.That(firstInternal, Is.Not.SameAs(secondInternal));
            Assert.That(GetInternalTimer(first), Is.SameAs(firstInternal));
            Assert.That(GetInternalTimer(second), Is.SameAs(secondInternal));
        }

        [Test]
        public void IsAlive_DefaultTimerIsNotAlive()
        {
            Assert.That(default(Timer).IsAlive, Is.False);
        }

        [Test]
        public void IsAlive_NonPeriodicTimerLivesUntilItsActionIsInvoked()
        {
            var calls = 0;
            var start = timerManager.Now.AddSeconds(5);
            var timer = Schedule().When(start).Action(() => calls++).Run();

            Assert.That(timer.IsAlive, Is.True);
            TickAt(start.AddTicks(-1));
            Assert.That(calls, Is.Zero);
            Assert.That(timer.IsAlive, Is.True);

            TickAt(start);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(timer.IsAlive, Is.False);
            TickAt(start.AddSeconds(10));
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(3)]
        public void IsAlive_PeriodicTimerLivesUntilInvocationLimitIsReached(int limit)
        {
            var calls = 0;
            var start = timerManager.Now.AddSeconds(5);
            var timer = Schedule().When(start).Every(TimeSpan.FromSeconds(2))
                .InvocationLimit(limit).Action(() => calls++).Run();

            Assert.That(timer.IsAlive, Is.True);
            for (var invocation = 0; invocation < limit; invocation++)
            {
                TickAt(start.AddSeconds(invocation * 2));
                Assert.That(calls, Is.EqualTo(invocation + 1));
                Assert.That(timer.IsAlive, Is.EqualTo(invocation + 1 < limit));
            }

            TickAt(start.AddSeconds((limit + 1) * 2));
            Assert.That(calls, Is.EqualTo(limit));
        }

        [Test]
        public void IsAlive_PeriodicTimerWithZeroInvocationLimitKeepsRunning()
        {
            var calls = 0;
            var start = timerManager.Now;
            var timer = Schedule().When(start).Every(TimeSpan.FromSeconds(2))
                .InvocationLimit(0).Action(() => calls++).Run();

            for (var invocation = 0; invocation < 10; invocation++)
            {
                TickAt(start.AddSeconds(invocation * 2));
                Assert.That(calls, Is.EqualTo(invocation + 1));
                Assert.That(timer.IsAlive, Is.True);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatingProperties_MatchBuilderConfiguration(bool repeating)
        {
            var period = TimeSpan.FromSeconds(7);
            var builder = Schedule().After(TimeSpan.FromSeconds(5));
            if (repeating) builder.Every(period);

            var timer = builder.Run();

            Assert.That(timer.IsRepeating, Is.EqualTo(repeating));
            Assert.That(timer.RepeatInterval, Is.EqualTo(repeating ? period : TimeSpan.Zero));
            Assert.That(GetInternalTimer(timer).IsRepeating, Is.EqualTo(timer.IsRepeating));
            Assert.That(GetInternalTimer(timer).RepeatInterval, Is.EqualTo(timer.RepeatInterval));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        public void InvocationLimit_MatchesBuilderConfiguration(int limit)
        {
            var timer = Schedule().After(TimeSpan.FromSeconds(5)).Every(TimeSpan.FromSeconds(2))
                .InvocationLimit(limit).Run();

            Assert.That(timer.InvocationLimit, Is.EqualTo(limit));
            Assert.That(GetInternalTimer(timer).InvocationLimit, Is.EqualTo(limit));
        }

        [TestCase(false, -5)]
        [TestCase(false, 0)]
        [TestCase(false, 5)]
        [TestCase(true, -5)]
        [TestCase(true, 0)]
        [TestCase(true, 5)]
        public void InvocationTimes_BeforeFirstInvocationMatchBuilderAndManagerTime(bool repeating, int delay)
        {
            var start = timerManager.Now.AddSeconds(delay);
            var builder = Schedule().When(start);
            if (repeating) builder.Every(TimeSpan.FromSeconds(2));
            var timer = builder.Run();

            AssertInvocationTimes(timer, start);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void InvocationTimes_PeriodicTimerTracksFirstMiddleAndLastInvocation(int invocation)
        {
            var start = timerManager.Now.AddSeconds(5);
            var calls = 0;
            var timer = Schedule().When(start).Every(TimeSpan.FromSeconds(2))
                .InvocationLimit(3).Action(() => calls++).Run();

            for (var previous = 0; previous < invocation; previous++)
                TickAt(start.AddSeconds(previous * 2));

            var scheduledTime = start.AddSeconds(invocation * 2);
            timeProvider.Now = scheduledTime.AddSeconds(-1);
            AssertInvocationTimes(timer, scheduledTime);
            timeProvider.Now = scheduledTime;
            AssertInvocationTimes(timer, scheduledTime);

            updateProvider.Tick(timing);

            Assert.That(calls, Is.EqualTo(invocation + 1));
            if (invocation == 2)
            {
                // Completed handles have no next invocation to inspect.
                Assert.That(timer.IsAlive, Is.False);
                return;
            }

            AssertInvocationTimes(timer, scheduledTime.AddSeconds(2));
            timeProvider.Now = scheduledTime.AddSeconds(1);
            AssertInvocationTimes(timer, scheduledTime.AddSeconds(2));
            Assert.That(timer.InvocationLimit, Is.EqualTo(3));
            Assert.That(timer.InvocationCount, Is.EqualTo(invocation + 1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TimeToNextInvocation_OverdueTimerClampsToZero(bool repeating)
        {
            var start = timerManager.Now.AddSeconds(5);
            var builder = Schedule().When(start);
            if (repeating) builder.Every(TimeSpan.FromSeconds(2));
            var timer = builder.Run();

            timeProvider.Now = start.AddSeconds(-1);
            AssertInvocationTimes(timer, start);
            timeProvider.Now = start.AddSeconds(3);
            AssertInvocationTimes(timer, start);
            Assert.That(timer.TimeToNextInvocation, Is.EqualTo(TimeSpan.Zero));
        }

        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        public void Actions_AllOverloadsRunInOrderWithCorrectParametersAndContext(bool repeating, int delay)
        {
            var start = timerManager.Now.AddSeconds(5);
            var calls = new List<string>();
            var contexts = new List<TimerInvocationContext>();
            var parameter = new object();
            var receivedParameters = new List<object>();
            var builder = Schedule().When(start);
            if (repeating) builder.Every(TimeSpan.FromSeconds(2)).InvocationLimit(3);
            var timer = AddAllActions(builder, calls, contexts, parameter, receivedParameters).Run();

            TickAt(start.AddTicks(-1));
            Assert.That(calls, Is.Empty);

            var count = repeating ? 3 : 1;
            for (var invocation = 0; invocation < count; invocation++)
            {
                var scheduledTime = start.AddSeconds(invocation * 2);
                var actualTime = scheduledTime.AddSeconds(delay);
                TickAt(actualTime);

                AssertActionCalls(calls, invocation + 1);
                Assert.That(receivedParameters.Count, Is.EqualTo((invocation + 1) * 2));
                Assert.That(receivedParameters, Is.All.SameAs(parameter));
                Assert.That(contexts.Count, Is.EqualTo((invocation + 1) * 2));
                AssertContext(contexts[invocation * 2], invocation, actualTime, scheduledTime);
                AssertContext(contexts[invocation * 2 + 1], invocation, actualTime, scheduledTime);

                updateProvider.Tick(timing);
                AssertActionCalls(calls, invocation + 1);
            }

            Assert.That(timer.IsAlive, Is.False);
            TickAt(start.AddSeconds(20));
            AssertActionCalls(calls, count);
        }

        [Test]
        public void Actions_OnlyRunDuringConfiguredPlayerLoopTiming()
        {
            var calls = 0;
            Schedule().When(timerManager.Now).Action(() => calls++).Run();

            foreach (PlayerLoopTiming otherTiming in Enum.GetValues(typeof(PlayerLoopTiming)))
            {
                if (otherTiming == timing) continue;
                updateProvider.Tick(otherTiming);
                Assert.That(calls, Is.Zero);
            }

            updateProvider.Tick(timing);
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase(MissingPeriodBehaviour.RunOnce, 1)]
        [TestCase(MissingPeriodBehaviour.RunAll, 4)]
        public void Actions_MissedPeriodsRunExpectedInvocationsAndPreserveSchedule(
            MissingPeriodBehaviour behaviour, int expectedInvocations)
        {
            var start = timerManager.Now.AddSeconds(5);
            var calls = new List<string>();
            var contexts = new List<TimerInvocationContext>();
            var parameter = new object();
            var receivedParameters = new List<object>();
            var builder = Schedule().When(start).Every(TimeSpan.FromSeconds(2))
                .MissingPeriodBehaviour(behaviour);
            var timer = AddAllActions(builder, calls, contexts, parameter, receivedParameters).Run();

            // Four scheduled invocations are due: start + 0, 2, 4, and 6 seconds.
            var actualTime = start.AddSeconds(7);
            TickAt(actualTime);

            AssertActionCalls(calls, expectedInvocations);
            Assert.That(receivedParameters.Count, Is.EqualTo(expectedInvocations * 2));
            Assert.That(receivedParameters, Is.All.SameAs(parameter));
            Assert.That(contexts.Count, Is.EqualTo(expectedInvocations * 2));
            for (var index = 0; index < contexts.Count; index++)
            {
                Assert.That(contexts[index].InvocationTime, Is.EqualTo(actualTime));
                Assert.That(contexts[index].InvocationDelay,
                    Is.EqualTo(actualTime - contexts[index].ScheduledTime));
                if (behaviour == MissingPeriodBehaviour.RunAll)
                    // ReSharper disable once PossibleLossOfFraction
                    AssertContext(contexts[index], index / 2, actualTime, start.AddSeconds(index / 2 * 2));
                else
                {
                    Assert.That(contexts[index].InvocationNumber, Is.Zero);
                    Assert.That(contexts[index].ScheduledTime, Is.InRange(start, actualTime));
                    Assert.That((contexts[index].ScheduledTime - start).Ticks % TimeSpan.FromSeconds(2).Ticks,
                        Is.Zero);
                    Assert.That(contexts[index].ScheduledTime, Is.EqualTo(contexts[0].ScheduledTime));
                }
            }

            Assert.That(timer.InvocationCount, Is.EqualTo(expectedInvocations));
            AssertInvocationTimes(timer, start.AddSeconds(8));
            updateProvider.Tick(timing);
            AssertActionCalls(calls, expectedInvocations);
            TickAt(start.AddSeconds(8).AddTicks(-1));
            AssertActionCalls(calls, expectedInvocations);
            TickAt(start.AddSeconds(8));
            AssertActionCalls(calls, expectedInvocations + 1);
            AssertInvocationTimes(timer, start.AddSeconds(10));
        }

        [TestCase(MissingPeriodBehaviour.RunOnce, 1)]
        [TestCase(MissingPeriodBehaviour.RunAll, 3)]
        public void Actions_MissedPeriodsRespectInvocationLimit(MissingPeriodBehaviour behaviour, int expectedCalls)
        {
            var calls = 0;
            var start = timerManager.Now;
            var timer = Schedule().When(start).Every(TimeSpan.FromSeconds(2)).InvocationLimit(3)
                .MissingPeriodBehaviour(behaviour).Action(() => calls++).Run();

            TickAt(start.AddSeconds(7));
            Assert.That(calls, Is.EqualTo(expectedCalls));
            Assert.That(timer.IsAlive, Is.EqualTo(behaviour == MissingPeriodBehaviour.RunOnce));
            if (behaviour == MissingPeriodBehaviour.RunOnce)
            {
                TickAt(start.AddSeconds(8));
                Assert.That(calls, Is.EqualTo(2));
                TickAt(start.AddSeconds(10));
                Assert.That(calls, Is.EqualTo(3));
                Assert.That(timer.IsAlive, Is.False);
            }

            TickAt(start.AddSeconds(20));
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void Actions_ExceptionsDoNotEscapeOrSkipActionsOrPreventRescheduling(
            [Values] ActionKind throwingAction, [Values] bool repeating)
        {
            timerManager.LogErrors = false;
            timerManager.CancelTimerOnException = false;
            var start = timerManager.Now.AddSeconds(5);
            var calls = new List<string>();
            var contexts = new List<TimerInvocationContext>();
            var parameter = new object();
            var receivedParameters = new List<object>();
            var attempts = 0;

            var builder = Schedule().When(start).Action(() => calls.Add("before"));
            if (repeating) builder.Every(TimeSpan.FromSeconds(2)).InvocationLimit(3);
            switch (throwingAction)
            {
                case ActionKind.WithoutParameter:
                    builder.Action(ThrowException);
                    break;
                case ActionKind.WithParameter:
                    builder.Action(parameter, _ => ThrowException());
                    break;
                case ActionKind.WithContext:
                    builder.Action(_ => ThrowException());
                    break;
                case ActionKind.WithContextAndParameter:
                    builder.Action(parameter, (_, _) => ThrowException());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(throwingAction), throwingAction, null);
            }

            var timer = AddAllActions(builder, calls, contexts, parameter, receivedParameters).Run();
            var expectedCalls = new List<string>();
            var count = repeating ? 3 : 1;
            for (var invocation = 0; invocation < count; invocation++)
            {
                var scheduledTime = start.AddSeconds(invocation * 2);
                Assert.DoesNotThrow(() => TickAt(scheduledTime));
                expectedCalls.AddRange(new[] { "before", "plain", "parameter", "context", "context and parameter" });
                Assert.That(attempts, Is.EqualTo(invocation + 1));
                Assert.That(calls, Is.EqualTo(expectedCalls));
                Assert.That(receivedParameters, Is.All.SameAs(parameter));
                Assert.That(contexts.Count, Is.EqualTo((invocation + 1) * 2));
                AssertContext(contexts[invocation * 2], invocation, scheduledTime, scheduledTime);
                AssertContext(contexts[invocation * 2 + 1], invocation, scheduledTime, scheduledTime);
                if (invocation + 1 < count) AssertInvocationTimes(timer, scheduledTime.AddSeconds(2));
            }

            Assert.That(timer.IsAlive, Is.False);
            return;

            void ThrowException()
            {
                attempts++;
                throw new InvalidOperationException("Expected timer action failure.");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Cancel_BeforeFirstInvocationStopsTimerImmediately(bool repeating)
        {
            var calls = 0;
            var start = timerManager.Now.AddSeconds(5);
            var builder = Schedule().When(start).Action(() => calls++);
            if (repeating) builder.Every(TimeSpan.FromSeconds(2));
            var timer = builder.Run();

            Assert.That(timer.IsAlive, Is.True);
            Assert.That(timer.Cancel(), Is.True);
            Assert.That(timer.IsAlive, Is.False);
            TickAt(start);
            TickAt(start.AddSeconds(10));
            Assert.That(calls, Is.Zero);
            Assert.That(timer.Cancel(), Is.False);
        }

        [Test]
        public void Cancel_AfterPeriodicInvocationPreventsFollowingInvocations()
        {
            var calls = 0;
            var start = timerManager.Now;
            var timer = Schedule().When(start).Every(TimeSpan.FromSeconds(2)).Action(() => calls++).Run();
            TickAt(start);
            Assert.That(calls, Is.EqualTo(1));

            Assert.That(timer.Cancel(), Is.True);
            Assert.That(timer.IsAlive, Is.False);
            TickAt(start.AddSeconds(2));
            TickAt(start.AddSeconds(20));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Cancel_InsideActionFinishesCurrentInvocationAndStopsFutureInvocations(
            [Values] bool repeating, [Values] MissingPeriodBehaviour behaviour)
        {
            var calls = new List<string>();
            var contexts = new List<TimerInvocationContext>();
            var parameter = new object();
            var receivedParameters = new List<object>();
            var cancelled = false;
            var start = timerManager.Now;
            Timer timer = default;
            var builder = Schedule().When(start).MissingPeriodBehaviour(behaviour)
                .Action(() => cancelled = timer.Cancel());
            if (repeating) builder.Every(TimeSpan.FromSeconds(2));
            timer = AddAllActions(builder, calls, contexts, parameter, receivedParameters).Run();

            // RunAll must also stop the remaining overdue invocations after cancellation.
            TickAt(start.AddSeconds(7));

            Assert.That(cancelled, Is.True);
            AssertActionCalls(calls, 1);
            Assert.That(receivedParameters.Count, Is.EqualTo(2));
            Assert.That(receivedParameters, Is.All.SameAs(parameter));
            Assert.That(contexts.Count, Is.EqualTo(2));
            Assert.That(timer.IsAlive, Is.False);
            TickAt(start.AddSeconds(20));
            AssertActionCalls(calls, 1);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Dispose_StopsTimersAndPreventsFurtherInvocations(bool repeating, bool invokeBeforeDisposal)
        {
            var calls = 0;
            var start = timerManager.Now.AddSeconds(5);
            var builder = Schedule().When(start).Action(() => calls++);
            if (repeating) builder.Every(TimeSpan.FromSeconds(2));
            var timer = builder.Run();
            if (invokeBeforeDisposal)
            {
                TickAt(start);
                Assert.That(calls, Is.EqualTo(1));
            }

            timerManager.Dispose();

            Assert.That(timer.IsAlive, Is.False);
            TickAt(start.AddSeconds(20));
            Assert.That(calls, Is.EqualTo(invokeBeforeDisposal ? 1 : 0));
        }

        [Test]
        public void Actions_CancelOnExceptionFinishesCurrentInvocationAndStopsCatchUp()
        {
            timerManager.LogErrors = false;
            timerManager.CancelTimerOnException = true;
            var failure = new InvalidOperationException("Expected failure.");
            var reported = new List<Exception>();
            var names = new List<string>();
            timerManager.SetErrorHandler((name, exception) =>
            {
                names.Add(name);
                reported.Add(exception);
            });
            var calls = 0;
            var timer = Schedule().When(timerManager.Now).Every(TimeSpan.FromSeconds(1))
                .MissingPeriodBehaviour(MissingPeriodBehaviour.RunAll).DebugName("failing timer")
                .Action(() => throw failure).Action(() => calls++).Run();

            Assert.DoesNotThrow(() => TickAt(timerManager.Now.AddSeconds(5)));

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(reported, Is.EqualTo(new[] { failure }));
            Assert.That(names, Is.EqualTo(new[] { "failing timer" }));
            Assert.That(timer.IsAlive, Is.False);
        }

        [Test]
        public void Actions_ThrowingErrorHandlerDoesNotSkipLaterActions()
        {
            timerManager.LogErrors = false;
            timerManager.SetErrorHandler((_, _) => throw new InvalidOperationException("Handler failure."));
            var calls = 0;
            Schedule().When(timerManager.Now).Action(() => throw new InvalidOperationException("Action failure."))
                .Action(() => calls++).Run();

            Assert.DoesNotThrow(() => updateProvider.Tick(timing));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Actions_CanCreateTimerAndCancelAnotherTimerDuringUpdate()
        {
            var calls = 0;
            Timer other = default;
            Timer created = default;
            Schedule().When(timerManager.Now).Action(() =>
            {
                Assert.That(other.Cancel(), Is.True);
                created = Schedule().When(timerManager.Now).Action(() => calls++).Run();
            }).Run();
            other = Schedule().After(TimeSpan.FromSeconds(10)).Action(() => calls += 100).Run();

            Assert.DoesNotThrow(() => updateProvider.Tick(timing));
            Assert.That(other.IsAlive, Is.False);
            Assert.That(created.IsAlive, Is.True);
            Assert.That(calls, Is.Zero);
            updateProvider.Tick(timing);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Cancel_InsideActionDoesNotReleaseInvocationDataBeforeRemainingActionsFinish()
        {
            var calls = new List<string>();
            Timer timer = default;
            Timer created = default;
            timer = Schedule().When(timerManager.Now).Action(() =>
            {
                Assert.That(timer.Cancel(), Is.True);
                Assert.That(timer.IsAlive, Is.False);
                created = Schedule().When(timerManager.Now).Action(() => calls.Add("new")).Run();
            }).Action(() => calls.Add("original")).Run();

            updateProvider.Tick(timing);
            Assert.That(calls, Is.EqualTo(new[] { "original" }));
            Assert.That(created.IsAlive, Is.True);
            updateProvider.Tick(timing);
            Assert.That(calls, Is.EqualTo(new[] { "original", "new" }));
        }

        [Test]
        public void CompletedTimer_PoolReuseResetsActionsCountAndConfiguration()
        {
            var oldCalls = 0;
            var first = Schedule().When(timerManager.Now).Every(TimeSpan.FromSeconds(1))
                .InvocationLimit(1).Action(() => oldCalls++).Run();
            var firstInternal = GetInternalTimer(first);
            updateProvider.Tick(timing);
            var newCalls = 0;
            var second = Schedule().When(timerManager.Now).Action(() => newCalls++).Run();

            Assert.That(GetInternalTimer(second), Is.SameAs(firstInternal));
            Assert.That(first.IsAlive, Is.False);
            Assert.That(first.Cancel(), Is.False);
            Assert.That(second.InvocationCount, Is.Zero);
            Assert.That(second.InvocationLimit, Is.Zero);
            Assert.That(second.IsRepeating, Is.False);
            updateProvider.Tick(timing);
            Assert.That(oldCalls, Is.EqualTo(1));
            Assert.That(newCalls, Is.EqualTo(1));
            Assert.That(second.IsAlive, Is.False);
        }

        [Test]
        public void Guards_TriggeredConditionCancelsTimerBeforeItIsDue()
        {
            var cancel = false;
            var calls = 0;
            var timer = Schedule().After(TimeSpan.FromSeconds(10)).CancelIf(() => cancel)
                .Action(() => calls++).Run();
            updateProvider.Tick(timing);
            Assert.That(timer.IsAlive, Is.True);

            cancel = true;
            updateProvider.Tick(timing);

            Assert.That(timer.IsAlive, Is.False);
            TickAt(timerManager.Now.AddSeconds(10));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void Dispose_InsideActionFinishesInvocationAndStopsTimers()
        {
            var calls = 0;
            var timer = Schedule().When(timerManager.Now).Every(TimeSpan.FromSeconds(1))
                .Action(() => timerManager.Dispose()).Action(() => calls++).Run();
            var other = Schedule().After(TimeSpan.FromSeconds(10)).Action(() => calls += 100).Run();

            Assert.DoesNotThrow(() => updateProvider.Tick(timing));

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(timer.IsAlive, Is.False);
            Assert.That(other.IsAlive, Is.False);
            TickAt(timerManager.Now.AddSeconds(20));
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Pause_FreezesRemainingTimeAndResumeShiftsDeadline(bool repeating)
        {
            var start = timerManager.Now;
            var calls = 0;
            var builder = Schedule().After(TimeSpan.FromSeconds(10)).Action(() => calls++);
            if (repeating) builder.Every(TimeSpan.FromSeconds(2)).InvocationLimit(2);
            var timer = builder.Run();
            Assert.That(timer.IsPaused, Is.False);
            timer.Resume();
            TickAt(start.AddSeconds(3));
            timer.Pause();

            Assert.That(timer.IsAlive, Is.True);
            Assert.That(timer.IsPausedSelf, Is.True);
            Assert.That(timer.IsPaused, Is.True);
            TickAt(start.AddSeconds(30));
            timer.Pause();
            Assert.That(calls, Is.Zero);
            Assert.That(timer.InvocationCount, Is.Zero);
            Assert.That(timer.TimeToNextInvocation, Is.EqualTo(TimeSpan.FromSeconds(7)));
            Assert.That(timer.NextInvocationTime, Is.EqualTo(start.AddSeconds(10)));

            timer.Resume();
            timer.Resume();
            Assert.That(timer.IsPausedSelf, Is.False);
            Assert.That(timer.IsPaused, Is.False);
            AssertInvocationTimes(timer, start.AddSeconds(37));
            TickAt(start.AddSeconds(36));
            Assert.That(calls, Is.Zero);
            TickAt(start.AddSeconds(37));
            Assert.That(calls, Is.EqualTo(1));
            if (repeating)
            {
                AssertInvocationTimes(timer, start.AddSeconds(39));
                TickAt(start.AddSeconds(39));
                Assert.That(calls, Is.EqualTo(2));
            }
            Assert.That(timer.IsAlive, Is.False);
        }

        [Test]
        public void Pause_RepeatedCyclesPreserveRemainingTime()
        {
            var start = timerManager.Now;
            var timer = Schedule().After(TimeSpan.FromSeconds(10)).Run();
            TickAt(start.AddSeconds(2));
            timer.Pause();
            TickAt(start.AddSeconds(5));
            timer.Resume();
            AssertInvocationTimes(timer, start.AddSeconds(13));
            TickAt(start.AddSeconds(6));
            timer.Pause();
            TickAt(start.AddSeconds(10));
            timer.Resume();
            AssertInvocationTimes(timer, start.AddSeconds(17));
        }

        [Test]
        public void Pause_BetweenRepetitionsDoesNotConsumeInvocationLimit([Values] MissingPeriodBehaviour behaviour)
        {
            var start = timerManager.Now;
            var calls = 0;
            var timer = Schedule().Every(TimeSpan.FromSeconds(10)).InvocationLimit(3)
                .MissingPeriodBehaviour(behaviour).Action(() => calls++).Run();
            TickAt(start);
            TickAt(start.AddSeconds(4));
            timer.Pause();
            TickAt(start.AddSeconds(104));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(timer.InvocationCount, Is.EqualTo(1));
            timer.Resume();
            AssertInvocationTimes(timer, start.AddSeconds(110));
            TickAt(start.AddSeconds(110));
            Assert.That(calls, Is.EqualTo(2));
            TickAt(start.AddSeconds(120));
            Assert.That(calls, Is.EqualTo(3));
            Assert.That(timer.IsAlive, Is.False);
        }

        [Test]
        public void Pause_InsideActionFinishesInvocationAndStopsCatchUp([Values] bool repeating,
            [Values] MissingPeriodBehaviour behaviour)
        {
            var start = timerManager.Now;
            var calls = 0;
            Timer timer = default;
            var builder = Schedule().Action(() => timer.Pause()).Action(() => calls++);
            if (repeating) builder.Every(TimeSpan.FromSeconds(2)).MissingPeriodBehaviour(behaviour);
            timer = builder.Run();

            TickAt(start.AddSeconds(7));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(timer.IsAlive, Is.EqualTo(repeating));
            Assert.That(timer.IsPaused, Is.EqualTo(repeating));
            TickAt(start.AddSeconds(17));
            Assert.That(calls, Is.EqualTo(1));
            timer.Resume();
            if (!repeating) return;
            var expected = start.AddSeconds(behaviour == MissingPeriodBehaviour.RunAll ? 12 : 18);
            Assert.That(timer.NextInvocationTime, Is.EqualTo(expected));
            TickAt(start.AddSeconds(18));
            Assert.That(calls, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Pause_CancellationGuardsStillRun(bool byGroup)
        {
            using var cancellation = new CancellationTokenSource();
            var group = timerManager.CreateGroup("paused");
            var calls = 0;
            var timer = Schedule().Group(group).CancellationToken(cancellation.Token).Action(() => calls++).Run();
            if (byGroup) group.Pause();
            else timer.Pause();
            cancellation.Cancel();
            TickAt(timerManager.Now);
            Assert.That(timer.IsAlive, Is.False);
            Assert.That(timer.IsPaused, Is.False);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void Pause_CancelledTimerDoesNotAffectReusedTimer()
        {
            var timer = Schedule().Run();
            var original = GetInternalTimer(timer);
            timer.Pause();
            Assert.That(timer.Cancel(), Is.True);
            var replacement = Schedule().Run();
            Assert.That(GetInternalTimer(replacement), Is.SameAs(original));
            timer.Pause();
            timer.Resume();
            Assert.That(timer.IsPaused, Is.False);
            Assert.That(timer.IsPausedSelf, Is.False);
            Assert.That(replacement.IsPaused, Is.False);
            TickAt(timerManager.Now);
            Assert.That(replacement.IsAlive, Is.False);
        }

        [Test]
        public void PauseResume_DefaultCompletedAndDisposedHandlesHaveNoEffect()
        {
            Timer empty = default;
            empty.Pause();
            empty.Resume();
            Assert.That(empty.IsPaused, Is.False);
            Assert.That(empty.IsPausedSelf, Is.False);
            var completed = Schedule().Run();
            TickAt(timerManager.Now);
            completed.Pause();
            completed.Resume();
            Assert.That(completed.IsPaused, Is.False);
            var disposed = Schedule().Run();
            disposed.Pause();
            timerManager.Dispose();
            disposed.Pause();
            disposed.Resume();
            Assert.That(disposed.IsPaused, Is.False);
            Assert.That(disposed.IsPausedSelf, Is.False);
        }

        [Test]
        public void Resume_UnrepresentableDeadlineCancelsTimer()
        {
            var timer = Schedule().When(DateTime.MaxValue).Run();
            timer.Pause();
            timeProvider.Now = timeProvider.Now.AddSeconds(1);
            Assert.DoesNotThrow(() => timer.Resume());
            Assert.That(timer.IsAlive, Is.False);
        }

        private TimerBuilder Schedule() => timerManager.Schedule().Timing(timing);

        private InternalTimer GetInternalTimer(Timer timer)
        {
            var internalTimer = timerManager.GetInternalTimer(timer.id);
            Assert.That(internalTimer, Is.Not.Null);
            return internalTimer;
        }

        private void TickAt(DateTime time)
        {
            timeProvider.Now = time;
            updateProvider.Tick(timing);
        }

        private void AssertInvocationTimes(Timer timer, DateTime expected)
        {
            var remaining = expected - timerManager.Now;
            Assert.That(timer.NextInvocationTime, Is.EqualTo(expected));
            Assert.That(GetInternalTimer(timer).NextInvocationTime, Is.EqualTo(expected));
            Assert.That(timer.TimeToNextInvocation, Is.EqualTo(remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining));
        }

        private static TimerBuilder AddAllActions(TimerBuilder builder, List<string> calls,
            List<TimerInvocationContext> contexts, object parameter, List<object> receivedParameters)
        {
            return builder.Action(() => calls.Add("plain"))
                .Action(parameter, value =>
                {
                    calls.Add("parameter");
                    receivedParameters.Add(value);
                })
                .Action(context =>
                {
                    calls.Add("context");
                    contexts.Add(context);
                })
                .Action(parameter, (context, value) =>
                {
                    calls.Add("context and parameter");
                    contexts.Add(context);
                    receivedParameters.Add(value);
                });
        }

        private static void AssertActionCalls(List<string> calls, int invocations)
        {
            var expected = new List<string>();
            for (var invocation = 0; invocation < invocations; invocation++)
                expected.AddRange(new[] { "plain", "parameter", "context", "context and parameter" });
            Assert.That(calls, Is.EqualTo(expected));
        }

        private static void AssertContext(TimerInvocationContext context, int invocationNumber,
            DateTime invocationTime, DateTime scheduledTime)
        {
            Assert.That(context.InvocationNumber, Is.EqualTo(invocationNumber));
            Assert.That(context.InvocationTime, Is.EqualTo(invocationTime));
            Assert.That(context.ScheduledTime, Is.EqualTo(scheduledTime));
            Assert.That(context.InvocationDelay, Is.EqualTo(invocationTime - scheduledTime));
        }
    }
}
