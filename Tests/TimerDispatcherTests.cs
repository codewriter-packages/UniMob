using System.Threading;
using NUnit.Framework;

namespace UniMob.Tests
{
    public class TimerDispatcherTests
    {
        private static TimerDispatcher MakeDispatcher() => new TimerDispatcher(
            Thread.CurrentThread.ManagedThreadId,
            ex => Assert.Fail(ex.Message));

        [Test]
        public void Invoke()
        {
            var dispatcher = MakeDispatcher();

            int value = 0;

            dispatcher.Invoke(() => ++value);
            Assert.AreEqual(0, value);
            Assert.AreEqual(false, dispatcher.ThreadedDirty);

            dispatcher.Tick(0);
            Assert.AreEqual(1, value);
        }

        [Test]
        public void ThreadedInvoke()
        {
            var dispatcher = MakeDispatcher();

            int value = 0;

            var thread = new Thread(() => dispatcher.Invoke(() => ++value));
            thread.Start();
            thread.Join();
            Assert.AreEqual(0, value);
            Assert.AreEqual(true, dispatcher.ThreadedDirty);

            dispatcher.Tick(0);
            Assert.AreEqual(false, dispatcher.ThreadedDirty);

            Assert.AreEqual(1, value);
        }

        [Test]
        public void InvokeDelayed()
        {
            var dispatcher = MakeDispatcher();

            int valueA = 0, valueB = 0;

            dispatcher.InvokeDelayed(5, () => ++valueA);
            dispatcher.InvokeDelayed(4, () => ++valueB);
            Assert.AreEqual(0, valueA);
            Assert.AreEqual(0, valueB);
            Assert.AreEqual(false, dispatcher.ThreadedDirty);

            dispatcher.Tick(4.9f);
            Assert.AreEqual(0, valueA);
            Assert.AreEqual(1, valueB);

            dispatcher.Tick(5f);
            Assert.AreEqual(1, valueA);
            Assert.AreEqual(1, valueB);
        }

        [Test]
        public void ThreadedInvokeDelayed()
        {
            var dispatcher = MakeDispatcher();

            int valueA = 0, valueB = 0;
            
            dispatcher.Tick(10f);

            var threadA = new Thread(() => dispatcher.InvokeDelayed(5, () => ++valueA)); // 15f
            var threadB = new Thread(() => dispatcher.InvokeDelayed(4, () => ++valueB)); // 14f
            threadA.Start();
            threadB.Start();
            threadA.Join();
            threadB.Join();

            Assert.AreEqual(0, valueA);
            Assert.AreEqual(0, valueB);
            Assert.AreEqual(true, dispatcher.ThreadedDirty);

            dispatcher.Tick(14.9f);
            Assert.AreEqual(false, dispatcher.ThreadedDirty);

            Assert.AreEqual(0, valueA);
            Assert.AreEqual(1, valueB);

            dispatcher.Tick(15.1f);
            Assert.AreEqual(1, valueA);
            Assert.AreEqual(1, valueB);
        }
    }
}