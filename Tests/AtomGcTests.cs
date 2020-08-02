using System;
using NUnit.Framework;

namespace UniMob.Tests
{
    public class AtomGcTests
    {
        [Test]
        public void NoSubscribed() => TestZone.Run(tick =>
        {
            var source = Atom.Value(1);
            var middle = Atom.Computed(() => source.Value + 1);
            var target = Atom.Computed(() => middle.Value + source.Value);

            Assert.AreEqual(3, target.Value);

            Assert.AreEqual(0, source.SubscribersCount());
            Assert.AreEqual(0, middle.SubscribersCount());
            Assert.AreEqual(0, target.SubscribersCount());
        });

        [Test]
        public void AutoUnsubscribed_Reaction() => TestZone.Run(tick =>
        {
            var source = Atom.Value(1);
            var middle = Atom.Computed(() => source.Value + 1);
            var target = Atom.Computed(() => middle.Value + source.Value);

            var run = Atom.AutoRun(() => target.Get());

            Assert.AreEqual(2, source.SubscribersCount());
            Assert.AreEqual(1, middle.SubscribersCount());
            Assert.AreEqual(1, target.SubscribersCount());

            run.Dispose();
            tick(0);

            Assert.AreEqual(0, source.SubscribersCount());
            Assert.AreEqual(0, middle.SubscribersCount());
            Assert.AreEqual(0, target.SubscribersCount());
        });

        [Test]
        public void KeepAliveComputed() => TestZone.Run(tick =>
        {
            var source = Atom.Value(1);
            var middle = Atom.Computed(() => source.Value + 1, keepAlive: true);
            var target = Atom.Computed(() => middle.Value + source.Value);

            var run = Atom.AutoRun(() => target.Get());

            Assert.AreEqual(2, source.SubscribersCount());
            Assert.AreEqual(1, middle.SubscribersCount());
            Assert.AreEqual(1, target.SubscribersCount());

            run.Dispose();
            tick(0);

            Assert.AreEqual(1, source.SubscribersCount());
            Assert.AreEqual(0, middle.SubscribersCount());
            Assert.AreEqual(0, target.SubscribersCount());

            middle.Deactivate();
            tick(0);

            Assert.AreEqual(0, source.SubscribersCount());
        });

        [Test]
        public void AutoUnsubscribed_MultiReaction() => TestZone.Run(tick =>
        {
            var source = Atom.Value(1);
            var middle = Atom.Computed(() => source.Value + 1);
            var target1 = Atom.Computed(() => middle.Value + 1);
            var target2 = Atom.Computed(() => middle.Value + 1);

            var run1 = Atom.AutoRun(() => target1.Get());
            var run2 = Atom.AutoRun(() => target2.Get());

            Assert.AreEqual(2, middle.SubscribersCount());

            run1.Dispose();
            tick(0);

            Assert.AreEqual(1, middle.SubscribersCount());

            run2.Dispose();
            tick(0);

            Assert.AreEqual(0, middle.SubscribersCount());
        });
    }
}