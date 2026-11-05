using System;
using NUnit.Framework;

namespace Damdor.Temporio.Tests
{
    public class ObjectPoolTets
    {
        private sealed class Item
        {
            public int Value;
        }

        private ObjectPool<Item> pool;
        private int createCalls;
        private int resetCalls;

        [SetUp]
        public void SetUp()
        {
            createCalls = 0;
            resetCalls = 0;
            pool = new ObjectPool<Item>(2, item =>
            {
                resetCalls++;
                item.Value = 0;
            }, () =>
            {
                createCalls++;
                return new Item { Value = 42 };
            });
        }

        [TestCase(-1, 0)]
        [TestCase(int.MinValue, 0)]
        [TestCase(0, 0)]
        [TestCase(3, 3)]
        public void Constructor_ClampsNegativeSizeWithoutCreatingElements(int size, int expectedSize)
        {
            var creations = 0;
            var instance = new ObjectPool<Item>(size, _ => { }, () =>
            {
                creations++;
                return new Item();
            });

            Assert.That(instance.MaxSize, Is.EqualTo(expectedSize));
            Assert.That(creations, Is.Zero);
        }

        [Test]
        public void Pop_WhenEmpty_CreatesDistinctElementsWithoutResettingThem()
        {
            var first = pool.Pop();
            var second = pool.Pop();

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null.And.Not.SameAs(first));
            Assert.That(first.Value, Is.EqualTo(42));
            Assert.That(second.Value, Is.EqualTo(42));
            Assert.That(createCalls, Is.EqualTo(2));
            Assert.That(resetCalls, Is.Zero);
        }

        [Test]
        public void Pop_WithCustomFactory_WhenEmpty_UsesOnlyCustomFactoryWithoutResetting()
        {
            var expected = new Item { Value = 7 };
            var customCreateCalls = 0;

            var actual = pool.Pop(() =>
            {
                customCreateCalls++;
                return expected;
            });

            Assert.That(actual, Is.SameAs(expected));
            Assert.That(actual.Value, Is.EqualTo(7));
            Assert.That(customCreateCalls, Is.EqualTo(1));
            Assert.That(createCalls, Is.Zero);
            Assert.That(resetCalls, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Pop_WhenItemsAvailable_ReusesThemInLastInFirstOutOrder(bool useCustomFactory)
        {
            var first = new Item();
            var second = new Item();
            pool.Release(first);
            pool.Release(second);
            Func<Item> unexpectedFactory = () => throw new InvalidOperationException("Factory should not be called.");

            var actualSecond = useCustomFactory ? pool.Pop(unexpectedFactory) : pool.Pop();
            var actualFirst = useCustomFactory ? pool.Pop(unexpectedFactory) : pool.Pop();

            Assert.That(actualSecond, Is.SameAs(second));
            Assert.That(actualFirst, Is.SameAs(first));
            Assert.That(createCalls, Is.Zero);
            Assert.That(resetCalls, Is.EqualTo(2));
        }

        [Test]
        public void Pop_WithCustomFactory_DoesNotReplaceDefaultFactory()
        {
            pool.Pop(() => new Item { Value = 7 });

            var actual = pool.Pop();

            Assert.That(actual.Value, Is.EqualTo(42));
            Assert.That(createCalls, Is.EqualTo(1));
        }

        [Test]
        public void Release_ResetsStateBeforeItemIsReusedOnEveryCycle()
        {
            var item = pool.Pop();

            pool.Release(item);

            Assert.That(item.Value, Is.Zero);
            Assert.That(resetCalls, Is.EqualTo(1));
            Assert.That(pool.Pop(), Is.SameAs(item));
            Assert.That(resetCalls, Is.EqualTo(1));

            item.Value = 99;
            pool.Release(item);

            Assert.That(item.Value, Is.Zero);
            Assert.That(resetCalls, Is.EqualTo(2));
            Assert.That(pool.Pop(), Is.SameAs(item));
            Assert.That(createCalls, Is.EqualTo(1));
        }

        [Test]
        public void Release_Null_DoesNotResetOrStoreAnItem()
        {
            Assert.DoesNotThrow(() => pool.Release(null));

            Assert.That(resetCalls, Is.Zero);
            Assert.That(pool.Pop(), Is.Not.Null);
            Assert.That(createCalls, Is.EqualTo(1));
        }

        [Test]
        public void Release_WhenFull_ResetsButDoesNotStoreOverflowItem()
        {
            pool.MaxSize = 1;
            var retained = new Item { Value = 1 };
            var overflow = new Item { Value = 2 };

            pool.Release(retained);
            pool.Release(overflow);

            Assert.That(overflow.Value, Is.Zero);
            Assert.That(resetCalls, Is.EqualTo(2));
            Assert.That(pool.Pop(), Is.SameAs(retained));
            Assert.That(pool.Pop(), Is.Not.SameAs(overflow).And.Not.SameAs(retained));
            Assert.That(createCalls, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Release_WithNonPositiveSize_ResetsButDoesNotRetainItem(int size)
        {
            pool.MaxSize = size;
            var item = new Item { Value = 9 };

            pool.Release(item);

            Assert.That(pool.MaxSize, Is.Zero);
            Assert.That(item.Value, Is.Zero);
            Assert.That(resetCalls, Is.EqualTo(1));
            Assert.That(pool.Pop(), Is.Not.SameAs(item));
            Assert.That(createCalls, Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void MaxSize_Decreasing_DiscardsExcessItemsImmediatelyWithoutResettingAgain(int size)
        {
            var first = new Item();
            var second = new Item();
            pool.Release(first);
            pool.Release(second);

            pool.MaxSize = size;

            Assert.That(pool.MaxSize, Is.EqualTo(Math.Max(size, 0)));
            Assert.That(resetCalls, Is.EqualTo(2));

            // Growing again must not restore items discarded by the previous resize.
            pool.MaxSize = 2;
            if (size == 1) Assert.That(pool.Pop(), Is.SameAs(first));
            var created = pool.Pop();
            Assert.That(created, Is.Not.SameAs(first).And.Not.SameAs(second));
            Assert.That(createCalls, Is.EqualTo(1));
            Assert.That(resetCalls, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void MaxSize_Increasing_AllowsMoreItemsWithoutPreallocating(int initialSize)
        {
            pool.MaxSize = initialSize;
            var first = new Item();
            var second = new Item();
            if (initialSize == 1) pool.Release(first);

            pool.MaxSize = 2;

            Assert.That(pool.MaxSize, Is.EqualTo(2));
            Assert.That(createCalls, Is.Zero);
            Assert.That(resetCalls, Is.EqualTo(initialSize));
            if (initialSize == 0) pool.Release(first);
            pool.Release(second);

            Assert.That(pool.Pop(), Is.SameAs(second));
            Assert.That(pool.Pop(), Is.SameAs(first));
            Assert.That(createCalls, Is.Zero);
        }

        [Test]
        public void MaxSize_AssigningSameSize_PreservesItemsAndOrder()
        {
            var first = new Item();
            var second = new Item();
            pool.Release(first);
            pool.Release(second);

            pool.MaxSize = pool.MaxSize;

            Assert.That(pool.Pop(), Is.SameAs(second));
            Assert.That(pool.Pop(), Is.SameAs(first));
            Assert.That(createCalls, Is.Zero);
            Assert.That(resetCalls, Is.EqualTo(2));
        }
    }
}
