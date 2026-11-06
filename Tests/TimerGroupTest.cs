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
        public void CheckIfTimerWithoutGroupHasNoGroup()
        {
            var timer = timerManager
                .Schedule()
                .Timing(timing)
                .Run();
            
            Assert.That(timer.TimerGroup, Is.EqualTo(default(TimerGroup)));
        }
        
        [Test]
        public void CheckNewGroupParameters()
        {
            var timerGroup = timerManager.CreateGroup("abc");
            
            Assert.That(timerGroup.IsAlive, Is.EqualTo(true));
            Assert.That(timerGroup.Name, Is.EqualTo("abc"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
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
            
            Assert.That(timer1.IsAlive, Is.EqualTo(false));
            Assert.That(timer2.IsAlive, Is.EqualTo(false));
            Assert.That(timer3.IsAlive, Is.EqualTo(true));
            Assert.That(timerGroup.IsAlive, Is.EqualTo(cancelType == CancelType.None));
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