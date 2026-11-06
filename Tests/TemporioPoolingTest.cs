using System.Threading;
using NUnit.Framework;

namespace Damdor.Temporio.Tests
{
    public class TemporioPoolingTest
    {
        [Test]
        public void CancellationTokenGuard_ReturnToPoolClearsCancelledToken()
        {
            using var pooling = new TemporioPooling();
            var guard = pooling.PopCancellationTokenTimeGuard();
            guard.Setup(new CancellationToken(true));
            Assert.That(guard.Check(), Is.False);

            guard.ReturnToPool(pooling);
            var reused = pooling.PopCancellationTokenTimeGuard();

            Assert.That(reused, Is.SameAs(guard));
            Assert.That(reused.Check(), Is.True);
            using var cancellation = new CancellationTokenSource();
            reused.Setup(cancellation.Token);
            Assert.That(reused.Check(), Is.True);
            cancellation.Cancel();
            Assert.That(reused.Check(), Is.False);
        }

        [Test]
        public void CancellationTokenGuard_UnusedBuilderReturnsGuardToPool()
        {
            using var updates = new ManualUpdateProvider();
            using var manager = new TimerManager(new ManualTimeProvider(default), updates);
            manager.Run();
            var builder = manager.Schedule().CancellationToken(new CancellationToken(true));
            var guard = manager.GetInternalTimerBuilder(builder.id).Guards[0];

            updates.Tick(PlayerLoopTiming.Update);
            var reused = manager.Pooling.PopCancellationTokenTimeGuard();

            Assert.That(reused, Is.SameAs(guard));
            Assert.That(reused.Check(), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancellationTokenGuard_CompletedOrCancelledTimerReturnsGuardToPool(bool cancel)
        {
            using var updates = new ManualUpdateProvider();
            using var manager = new TimerManager(new ManualTimeProvider(default), updates);
            using var cancellation = new CancellationTokenSource();
            manager.Run();
            var builder = manager.Schedule().CancellationToken(cancellation.Token);
            var guard = manager.GetInternalTimerBuilder(builder.id).Guards[0];
            var timer = builder.Run();
            Assert.That(manager.GetInternalTimer(timer.id).Guards[0], Is.SameAs(guard));

            if (cancel) cancellation.Cancel();
            updates.Tick(PlayerLoopTiming.Update);
            var reused = manager.Pooling.PopCancellationTokenTimeGuard();

            Assert.That(timer.IsAlive, Is.False);
            Assert.That(reused, Is.SameAs(guard));
            Assert.That(reused.Check(), Is.True);
        }
    }
}
