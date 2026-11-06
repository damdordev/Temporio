using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Damdor.Temporio.Tests
{
    [TestFixture(PlayerLoopTiming.Update)]
    [TestFixture(PlayerLoopTiming.FixedUpdate)]
    [TestFixture(PlayerLoopTiming.LateUpdate)]
    public class TimerGroupTest
    {
        public enum CancelType
        {
            None,
            Cancel,
            Dispose
        }
        
        private readonly PlayerLoopTiming timing;
        private TimerManager timerManager;
        private ManualTimeProvider timeProvider;
        private ManualUpdateProvider updateProvider;
        
        public TimerGroupTest(PlayerLoopTiming timing)
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
        
        [Test]
        public void CheckIfGroupIsVisibleInTimer()
        {
            var timerGroup = timerManager.CreateGroup("abc");
            var timer = timerManager
                .Schedule()
                .Group(timerGroup)
                .Timing(timing)
                .Run();
            
            var timer2 = timerManager
                .Schedule()
                .Timing(timing)
                .Run();
            
            Assert.That(timer.TimerGroup, Is.EqualTo(timerGroup));
            Assert.That(timer.TimerGroup.Name, Is.EqualTo(timerGroup.Name));
            Assert.That(timer2.TimerGroup, Is.Not.EqualTo(timerGroup));
        }
        
        [Test]
        public void CheckNewGroupParameters()
        {
            var timerGroup = timerManager.CreateGroup("abc");
            
            Assert.That(timerGroup.IsAlive, Is.EqualTo(true));
            Assert.That(timerGroup.Name, Is.EqualTo("abc"));
        }

        [Test]
        [TestCase(CancelType.None)]
        [TestCase(CancelType.Cancel)]
        [TestCase(CancelType.Dispose)]
        public void TestCancelAndDispose(CancelType cancelType)
        {
            var timerGroup = timerManager.CreateGroup("abc");
            var timer1 = timerManager
                .Schedule()
                .Group(timerGroup)
                .Timing(timing)
                .Run();
            
            var timer2 = timerManager
                .Schedule()
                .Group(timerGroup)
                .Timing(timing)
                .Run();
            
            var timer3 = timerManager
                .Schedule()
                .Timing(timing)
                .Run();

            Cancel(timerGroup, cancelType);
            
            Assert.That(timer1.IsAlive, Is.EqualTo(cancelType == CancelType.None));
            Assert.That(timer2.IsAlive, Is.EqualTo(cancelType == CancelType.None));
            Assert.That(timer3.IsAlive, Is.EqualTo(true));
            Assert.That(timerGroup.IsAlive, Is.EqualTo(cancelType != CancelType.Dispose));
        }
        
        [Test]
        [TestCase(CancelType.None)]
        [TestCase(CancelType.Cancel)]
        [TestCase(CancelType.Dispose)]
        public void TestGetTimersInGroup(CancelType cancelType)
        {
            var timerGroup = timerManager.CreateGroup("abc");
            var timer1 = timerManager
                .Schedule()
                .Group(timerGroup)
                .Timing(timing)
                .Run();
            
            var timer2 = timerManager
                .Schedule()
                .Group(timerGroup)
                .Timing(timing)
                .Run();
            
            var timer3 = timerManager
                .Schedule()
                .Timing(timing)
                .Run();
            
            Cancel(timerGroup, cancelType);
            
            var timers = new List<Timer>();
            timerGroup.GetTimersInGroup(timers);

            if (cancelType == CancelType.None)
            {
                CollectionAssert.AreEqual(timerGroup.GetTimersInGroup(), new[] { timer1, timer2 });
                CollectionAssert.AreEqual(timers, new[] { timer1, timer2 });   
            }
            else
            {
                CollectionAssert.IsEmpty(timerGroup.GetTimersInGroup());
                CollectionAssert.IsEmpty(timers);
            }
        }

        [Test]
        public void Pause_OnlyAffectsGroupTimersAndRepeatedCallsHaveNoEffect()
        {
            var start = timerManager.Now;
            var group = timerManager.CreateGroup("paused");
            var calls = 0;
            var first = timerManager.Schedule().Timing(timing).Group(group).After(TimeSpan.FromSeconds(10))
                .Action(() => calls++).Run();
            var second = timerManager.Schedule().Timing(timing).Group(group).After(TimeSpan.FromSeconds(12))
                .Action(() => calls++).Run();
            timerManager.Schedule().Timing(timing).After(TimeSpan.FromSeconds(10)).Action(() => calls += 100).Run();
            group.Resume();
            TickAt(start.AddSeconds(3));
            group.Pause();
            Assert.That(first.IsPaused, Is.True);
            Assert.That(first.IsPausedSelf, Is.False);
            Assert.That(second.IsPaused, Is.True);
            TickAt(start.AddSeconds(30));
            group.Pause();
            Assert.That(calls, Is.EqualTo(100));
            Assert.That(group.GetTimersInGroup(), Has.Count.EqualTo(2));
            Assert.That(first.TimeToNextInvocation, Is.EqualTo(TimeSpan.FromSeconds(7)));
            group.Resume();
            group.Resume();
            Assert.That(first.IsPaused, Is.False);
            Assert.That(second.IsPaused, Is.False);
            Assert.That(first.NextInvocationTime, Is.EqualTo(start.AddSeconds(37)));
            Assert.That(second.NextInvocationTime, Is.EqualTo(start.AddSeconds(39)));
            TickAt(start.AddSeconds(37));
            Assert.That(calls, Is.EqualTo(101));
            TickAt(start.AddSeconds(39));
            Assert.That(calls, Is.EqualTo(102));
        }

        [Test]
        public void Pause_OverlappingTimerAndGroupPausesCountDurationOnce([Values] bool pauseSelfFirst,
            [Values] bool resumeSelfFirst)
        {
            var start = timerManager.Now;
            var group = timerManager.CreateGroup("overlap");
            var calls = 0;
            var timer = timerManager.Schedule().Timing(timing).Group(group).After(TimeSpan.FromSeconds(10))
                .Action(() => calls++).Run();
            TickAt(start.AddSeconds(2));
            if (pauseSelfFirst) timer.Pause();
            else group.Pause();
            TickAt(start.AddSeconds(4));
            if (pauseSelfFirst) group.Pause();
            else timer.Pause();
            Assert.That(timer.IsPausedSelf, Is.True);
            TickAt(start.AddSeconds(6));
            if (resumeSelfFirst) timer.Resume();
            else group.Resume();
            Assert.That(timer.IsPaused, Is.True);
            Assert.That(timer.IsPausedSelf, Is.EqualTo(!resumeSelfFirst));
            Assert.That(timer.TimeToNextInvocation, Is.EqualTo(TimeSpan.FromSeconds(8)));
            TickAt(start.AddSeconds(8));
            if (resumeSelfFirst) group.Resume();
            else timer.Resume();
            Assert.That(timer.IsPaused, Is.False);
            Assert.That(timer.NextInvocationTime, Is.EqualTo(start.AddSeconds(16)));
            TickAt(start.AddSeconds(15));
            Assert.That(calls, Is.Zero);
            TickAt(start.AddSeconds(16));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Pause_NewTimersInPausedGroupStartPaused()
        {
            var start = timerManager.Now;
            var group = timerManager.CreateGroup("paused before scheduling");
            group.Pause();
            TickAt(start.AddSeconds(3));
            var calls = 0;
            var timer = timerManager.Schedule().Timing(timing).Group(group).After(TimeSpan.FromSeconds(10))
                .Action(() => calls++).Run();
            Assert.That(timer.IsPaused, Is.True);
            Assert.That(timer.IsPausedSelf, Is.False);
            timer.Resume();
            Assert.That(timer.IsPaused, Is.True);
            TickAt(start.AddSeconds(20));
            Assert.That(calls, Is.Zero);
            Assert.That(timer.TimeToNextInvocation, Is.EqualTo(TimeSpan.FromSeconds(10)));
            group.Resume();
            Assert.That(timer.NextInvocationTime, Is.EqualTo(start.AddSeconds(30)));
            TickAt(start.AddSeconds(30));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Pause_InsideActionFinishesInvocationAndStopsGroupCatchUp()
        {
            var start = timerManager.Now;
            var group = timerManager.CreateGroup("pause from callback");
            var calls = 0;
            var first = timerManager.Schedule().Timing(timing).Group(group).Every(TimeSpan.FromSeconds(1))
                .MissingPeriodBehaviour(MissingPeriodBehaviour.RunAll)
                .Action(() => group.Pause()).Action(() => calls++).Run();
            var second = timerManager.Schedule().Timing(timing).Group(group).Action(() => calls += 100).Run();
            TickAt(start.AddSeconds(5));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(first.InvocationCount, Is.EqualTo(1));
            Assert.That(first.IsPaused, Is.True);
            Assert.That(second.IsPaused, Is.True);
            TickAt(start.AddSeconds(10));
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase(CancelType.Cancel)]
        [TestCase(CancelType.Dispose)]
        public void Pause_CancelAndDisposeStillCancelTimers(CancelType type)
        {
            var group = timerManager.CreateGroup("cancel while paused");
            var timer = timerManager.Schedule().Timing(timing).Group(group).Run();
            group.Pause();
            Cancel(group, type);
            group.Resume();
            Assert.That(timer.IsAlive, Is.False);
            Assert.That(timer.IsPaused, Is.False);
        }

        [Test]
        public void PauseResume_DefaultDisposedAndReusedGroupsAreSafe()
        {
            TimerGroup empty = default;
            empty.Pause();
            empty.Resume();
            var oldGroup = timerManager.CreateGroup("old");
            var original = timerManager.GetInternalGroupId(oldGroup.id);
            oldGroup.Pause();
            oldGroup.Dispose();
            var group = timerManager.CreateGroup("new");
            Assert.That(timerManager.GetInternalGroupId(group.id), Is.SameAs(original));
            var timer = timerManager.Schedule().Timing(timing).Group(group).Run();
            oldGroup.Pause();
            oldGroup.Resume();
            Assert.That(timer.IsPaused, Is.False);
            group.Pause();
            timerManager.Dispose();
            Assert.DoesNotThrow(() => group.Pause());
            Assert.DoesNotThrow(() => group.Resume());
        }

        private void TickAt(DateTime time)
        {
            timeProvider.Now = time;
            updateProvider.Tick(timing);
        }

        private void Cancel(TimerGroup group, CancelType type)
        {
            switch (type)
            {
                case CancelType.Cancel:
                    group.Cancel();
                    break;
                case CancelType.Dispose:
                    group.Dispose();
                    break;
                case CancelType.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        
    }
}
