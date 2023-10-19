using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace UniMob.Tests
{
    public class LifetimeTests
    {
        private LifetimeController _controller;

        [SetUp]
        public void SetUp()
        {
            _controller = new LifetimeController();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
        }

        [Test]
        public void WhenDisposing_AndNoRegistrations_ThenNothing()
        {
            _controller.Dispose();
            _controller.Dispose();
        }

        [Test]
        public void TestEquals()
        {
            Lifetime eternal = default;
            Assert.AreEqual(Lifetime.Eternal, eternal);
            Assert.AreEqual(Lifetime.Eternal, Lifetime.Eternal);
            Assert.AreEqual(eternal, eternal);

            Assert.IsTrue(Lifetime.Eternal == eternal);

            Assert.AreNotEqual(Lifetime.Eternal, Lifetime.Terminated);
            Assert.IsFalse(Lifetime.Eternal == Lifetime.Terminated);
            Assert.IsFalse(eternal == Lifetime.Terminated);
        }

        [Test]
        public void TestTerminated()
        {
            Assert.IsTrue(Lifetime.Terminated.IsDisposed);
        }

        [Test]
        public void WhenDisposing_AndActionRegistered_ThenActionInvoked()
        {
            var runs = "";
            var action = new Action(() => runs += "R");
            _controller.Register(action);

            _controller.Dispose();

            Assert.AreEqual("R", runs);
        }

        [Test]
        public void WhenDisposing_AndMultipleRegistrations_ThenDisposesInReversedOrder()
        {
            var log = new List<int>();

            _controller.Lifetime.Register(() => log.Add(1));
            _controller.Lifetime.Register(() => log.Add(2));
            _controller.Lifetime.Register(() => log.Add(3));

            _controller.Dispose();

            Assert.AreEqual(new[] {3, 2, 1}, log.ToArray());
        }

        [Test]
        public void WhenDisposing_AndNestedRegistrations_ThenDisposesInReversedOrder()
        {
            var log = new List<int>();

            _controller.Lifetime.Register(() => log.Add(1));
            _controller.Lifetime.CreateNested().Lifetime.Register(() => log.Add(2));
            _controller.Lifetime.Register(() => log.Add(3));

            _controller.Dispose();

            Assert.AreEqual(new[] {3, 2, 1}, log.ToArray());
        }

        [Test]
        public void WhenDisposing_AndAnyRegistered_ThenRegistrationsNull()
        {
            _controller.Register(() => { });
            _controller.Register(() => { });

            _controller.Dispose();

            Assert.IsNull(_controller.registrations);
        }

        [Test]
        public void WhenDisposingNestedLifetime_ThenRootLifetimeSteelAlive()
        {
            var nestedDisposer = _controller.Lifetime.CreateNested(out var nestedLifetime);

            nestedDisposer.Dispose();

            Assert.IsFalse(_controller.IsDisposed);
        }

        [Test]
        public void WhenDisposingRootLifetime_AndNestedCreated_ThenNestedLifetimeDisposed()
        {
            _controller.Lifetime.CreateNested(out var nestedLifetime);

            _controller.Dispose();

            Assert.IsTrue(nestedLifetime.IsDisposed);
        }

        [Test]
        public void WhenRegisteringOnEternalLifetime_ThenRegistrationsIgnored()
        {
            Lifetime.Eternal.Register(() => { });

            Assert.True(Lifetime.Eternal.IsEternal);
            Assert.IsNull(((LifetimeController) LifetimeController.Eternal).registrations);
        }

        [Test]
        public void WhenDisposingEternalLifetime_ThenExceptionThrown()
        {
            Assert.Throws<InvalidOperationException>(() => LifetimeController.Eternal.Dispose());
        }

        [Test]
        public void CreateCancellationToken()
        {
            CancellationToken token = _controller.Lifetime;

            Assert.IsFalse(token.IsCancellationRequested);

            _controller.Dispose();

            Assert.IsTrue(token.IsCancellationRequested);
        }

        [Test]
        public void CreateCancellationTokenFromDisposedLifetime()
        {
            _controller.Dispose();

            CancellationToken token = _controller.Lifetime;

            Assert.IsTrue(token.IsCancellationRequested);
        }

        [Test]
        public void CancellationTokenOfTerminatedLifetimeIsCancelled()
        {
            CancellationToken token = Lifetime.Terminated;

            Assert.IsTrue(token.IsCancellationRequested);
        }

        [Test]
        public void AllocateRegistrationTest()
        {
            Core.ArrayPool<object>.ClearCache();

            using (_controller.Lifetime.CreateNested(out var nested))
            {
                nested.Register(() => { });
            }

            Assert.AreEqual(1, Core.ArrayPool<object>.GetPool(2).Count);
            Assert.That(Core.ArrayPool<object>.GetPool(2), Is.All.All.Null);

            Assert.AreEqual(0, Core.ArrayPool<object>.GetPool(4).Count);
        }

        [Test]
        public void GrowRegistrationTest()
        {
            Core.ArrayPool<object>.ClearCache();

            using (_controller.Lifetime.CreateNested(out var nested))
            {
                nested.Register(() => { });
                nested.Register(() => { });
                nested.Register(() => { });
            }

            Assert.AreEqual(1, Core.ArrayPool<object>.GetPool(2).Count);
            Assert.That(Core.ArrayPool<object>.GetPool(2), Is.All.All.Null);

            Assert.AreEqual(1, Core.ArrayPool<object>.GetPool(4).Count);
            Assert.That(Core.ArrayPool<object>.GetPool(4), Is.All.All.Null);
        }

        [Test]
        public void CompressRegistrationTest()
        {
            var action = new Action(() => { });

            Core.ArrayPool<object>.ClearCache();

            using (_controller.Lifetime.CreateNested(out var nested))
            {
                nested.Register(action);
                nested.Register(() => { });
                nested.UnregisterInternal(action);
                nested.Register(() => { });
            }

            Assert.AreEqual(1, Core.ArrayPool<object>.GetPool(2).Count);
            Assert.That(Core.ArrayPool<object>.GetPool(2), Is.All.All.Null);

            Assert.AreEqual(0, Core.ArrayPool<object>.GetPool(4).Count);
        }


        [Test]
        public void CompressTest()
        {
            var action1 = new Action(() => { });
            var action2 = new Action(() => { });
            var action3 = new Action(() => { });
            var action4 = new Action(() => { });

            Core.ArrayPool<object>.ClearCache();

            using (_controller.Lifetime.CreateNested(out var nested))
            {
                nested.Register(action1);
                nested.Register(action2);
                nested.Register(action3);
                nested.Register(action4);
                nested.UnregisterInternal(action1);
                nested.UnregisterInternal(action2);
                nested.UnregisterInternal(action3);
                nested.Register(() => { });
            }

            Assert.AreEqual(1, Core.ArrayPool<object>.GetPool(2).Count);
            Assert.That(Core.ArrayPool<object>.GetPool(2), Is.All.All.Null);

            Assert.AreEqual(1, Core.ArrayPool<object>.GetPool(4).Count);
            Assert.That(Core.ArrayPool<object>.GetPool(4), Is.All.All.Null);
        }
    }
}