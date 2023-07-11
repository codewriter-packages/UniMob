using System;
using System.Collections.Generic;
using NUnit.Framework;
using UniMob.Core;

namespace UniMob.Tests
{
    public class AtomSinkTests
    {
        private LifetimeController _lifetimeController;

        public Lifetime Lifetime => _lifetimeController.Lifetime;

        [SetUp]
        public void SetUp()
        {
            _lifetimeController = new LifetimeController();
        }

        [TearDown]
        public void TearDown()
        {
            _lifetimeController.Dispose();
        }

        [Test]
        public void InactiveByDefault()
        {
            var subscribeTimes = 0;
            var target = Atom.FromSink(Lifetime, 0, _ => ++subscribeTimes);

            AtomAssert.That(target).IsNotActive();
            AtomAssert.That(target).StateIs(AtomState.Obsolete);
            Assert.AreEqual(0, subscribeTimes);
        }

        [Test]
        public void ActivateAndSubscribeOnceOnUnTrackedRead()
        {
            var subscribeTimes = 0;
            var target = Atom.FromSink(Lifetime, 0, _ => ++subscribeTimes);

            target.Get();
            target.Invalidate();
            target.Get();

            AtomAssert.That(target).IsActive();
            AtomAssert.That(target).StateIs(AtomState.Actual);
            Assert.AreEqual(1, subscribeTimes);
        }

        [Test]
        public void ActivateAndSubscribeOnceOnTrackedRead()
        {
            var subscribeTimes = 0;
            var target = Atom.FromSink(Lifetime, 0, _ => ++subscribeTimes);

            Atom.Reaction(Lifetime, () => target.Get());
            target.Invalidate();
            Atom.Reaction(Lifetime, () => target.Get());

            AtomAssert.That(target).IsActive();
            AtomAssert.That(target).StateIs(AtomState.Actual);
            Assert.AreEqual(1, subscribeTimes);
        }

        [Test]
        public void DeactivateAndUnsubscribeOnLifetimeDispose()
        {
            var unsubscribeTimes = 0;
            Atom<int> target;

            using (var nested = Lifetime.CreateNested())
            {
                target = Atom.FromSink(nested.Lifetime, 0, _ => { }, () => ++unsubscribeTimes);
                target.Get();
            }

            AtomAssert.That(target).IsNotActive();
            AtomAssert.That(target).StateIs(AtomState.Obsolete);
            Assert.AreEqual(1, unsubscribeTimes);
        }

        [Test]
        public void DeactivateAndUnsubscribeOnAtomDeactivation()
        {
            var unsubscribeTimes = 0;

            var target = Atom.FromSink(Lifetime, 0, _ => { }, () => ++unsubscribeTimes);

            target.Get();
            target.Deactivate();

            AtomAssert.That(target).IsNotActive();
            AtomAssert.That(target).StateIs(AtomState.Obsolete);
            Assert.AreEqual(1, unsubscribeTimes);
        }

        [Test]
        public void DefaultValue()
        {
            var target = Atom.FromSink(Lifetime, 0, _ => { });

            Assert.AreEqual(0, target.Value);
            AtomAssert.That(target).StateIs(AtomState.Actual);
        }

        [Test]
        public void ImmediateSetValue()
        {
            var target = Atom.FromSink(Lifetime, 0, sink => sink.SetValue(1));

            Assert.AreEqual(1, target.Value);
            AtomAssert.That(target).StateIs(AtomState.Actual);
        }

        [Test]
        public void ImmediateSetException()
        {
            var exception = new Exception();
            var target = Atom.FromSink(Lifetime, 0, sink => sink.SetException(exception));

            var thrownEx = Assert.Throws<Exception>(() => target.Get());
            Assert.AreEqual(exception, thrownEx);
            AtomAssert.That(target).StateIs(AtomState.Actual);
        }

        [Test]
        public void SetValues()
        {
            var exception = new Exception();

            AtomSink<int> sinkShared = null;
            var target = Atom.FromSink(Lifetime, 0, sink => sinkShared = sink);

            Assert.AreEqual(0, target.Value);

            sinkShared.SetValue(1);
            Assert.AreEqual(1, target.Value);
            AtomAssert.That(target).StateIs(AtomState.Actual);

            sinkShared.SetException(exception);
            var thrownEx = Assert.Throws<Exception>(() => target.Get());
            Assert.AreEqual(exception, thrownEx);
            AtomAssert.That(target).StateIs(AtomState.Actual);

            sinkShared.SetValue(3);
            Assert.AreEqual(3, target.Value);
            AtomAssert.That(target).StateIs(AtomState.Actual);
        }

        [Test]
        public void ReactionOnImmediateSetValue()
        {
            var runs = 0;
            var target = Atom.FromSink(Lifetime, 0, sink => sink.SetValue(1));
            var reaction = Atom.Reaction(Lifetime, () =>
            {
                target.Get();
                ++runs;
            });

            Assert.AreEqual(1, runs);
            AtomAssert.That(target).StateIs(AtomState.Actual);
            AtomAssert.That(reaction).StateIs(AtomState.Actual);

            AtomScheduler.Sync();

            Assert.AreEqual(1, runs);
            AtomAssert.That(target).StateIs(AtomState.Actual);
            AtomAssert.That(reaction).StateIs(AtomState.Actual);
        }

        [Test]
        public void ReactionOnImmediateSetException()
        {
            var runs = 0;
            var fails = 0;
            var target = Atom.FromSink(Lifetime, 0, sink => sink.SetException(new Exception()));
            var reaction = Atom.Reaction(Lifetime, () =>
            {
                target.Get();
                ++runs;
            }, exceptionHandler: _ => ++fails);

            Assert.AreEqual(0, runs);
            Assert.AreEqual(1, fails);
            AtomAssert.That(target).StateIs(AtomState.Actual);
            AtomAssert.That(reaction).StateIs(AtomState.Actual);

            AtomScheduler.Sync();

            Assert.AreEqual(0, runs);
            Assert.AreEqual(1, fails);
            AtomAssert.That(target).StateIs(AtomState.Actual);
            AtomAssert.That(reaction).StateIs(AtomState.Actual);
        }

        [Test]
        public void ReactionSetValues()
        {
            var runs = 0;
            var fails = 0;
            var exception = new Exception();

            AtomSink<int> sinkShared = null;
            var target = Atom.FromSink(Lifetime, 0, sink => sinkShared = sink);
            var reaction = Atom.Reaction(Lifetime, () =>
            {
                target.Get();
                ++runs;
            }, exceptionHandler: _ => ++fails);

            Assert.AreEqual(1, runs);
            Assert.AreEqual(0, fails);

            sinkShared.SetValue(1);
            AtomScheduler.Sync();
            Assert.AreEqual(2, runs);
            Assert.AreEqual(0, fails);
            AtomAssert.That(target).StateIs(AtomState.Actual);
            AtomAssert.That(reaction).StateIs(AtomState.Actual);

            sinkShared.SetException(exception);
            AtomScheduler.Sync();
            Assert.AreEqual(2, runs);
            Assert.AreEqual(1, fails);
            AtomAssert.That(target).StateIs(AtomState.Actual);
            AtomAssert.That(reaction).StateIs(AtomState.Actual);

            sinkShared.SetValue(3);
            AtomScheduler.Sync();
            Assert.AreEqual(3, runs);
            Assert.AreEqual(1, fails);
            AtomAssert.That(target).StateIs(AtomState.Actual);
            AtomAssert.That(reaction).StateIs(AtomState.Actual);
        }
    }
}