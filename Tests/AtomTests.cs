using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace UniMob.Tests
{
    public class AtomTests
    {
        [Test]
        public void NoActivationWithoutSubscribers() => TestZone.Run(tick =>
        {
            var runs = "";
            var activation = "";

            var atom = Atom.Computed(
                pull: () =>
                {
                    runs += "R";
                    return 1;
                },
                onActive: () => activation += "A",
                onInactive: () => activation += "D");

            atom.Get();
            atom.Get();
            atom.Get();
            Assert.AreEqual("RRR", runs);
            Assert.AreEqual("", activation);
        });

        [Test]
        public void ManualActivation() => TestZone.Run(tick =>
        {
            var activation = "";

            var atom = Atom.Computed(
                pull: () => 1,
                keepAlive: true,
                onActive: () => activation += "A",
                onInactive: () => activation += "D");

            Assert.AreEqual("", activation);

            atom.Get();
            Assert.AreEqual("A", activation);

            atom.Deactivate();
            Assert.AreEqual("AD", activation);
        });

        [Test]
        public void AutoActivation() => TestZone.Run(tick =>
        {
            var activation = "";

            var atom = Atom.Computed(
                pull: () => 1,
                onActive: () => activation += "A",
                onInactive: () => activation += "D");

            var listener = Atom.Computed(() => atom.Value + 1, keepAlive: true);

            Assert.AreEqual("", activation);

            listener.Get();
            Assert.AreEqual("A", activation);

            listener.Deactivate();
            Assert.AreEqual("A", activation);

            tick(0);
            Assert.AreEqual("AD", activation);
        });

        [Test]
        public void Deactivation() => TestZone.Run(tick =>
        {
            var activation = "";

            var source = Atom.Value(
                0,
                onActive: () => activation += "S",
                onInactive: () => activation += "s");

            var middle = Atom.Computed(
                () => source.Value + 1,
                onActive: () => activation += "M",
                onInactive: () => activation += "m");

            var target = Atom.Computed(
                () => middle.Value + 1,
                onActive: () => activation += "T",
                onInactive: () => activation += "t");

            target.Get();
            Assert.AreEqual("", activation);

            var autoRun = Atom.AutoRun(() => target.Get());
            Assert.AreEqual("TMS", activation);

            autoRun.Dispose();
            Assert.AreEqual("TMS", activation);

            tick(0);
            Assert.AreEqual("TMStms", activation);
        });

        [Test]
        public void NoReactivationDuringPulling() => TestZone.Run(tick =>
        {
            var activation = "";

            var activationSource = Atom.Computed(
                pull: () => 1,
                onActive: () => activation += "A",
                onInactive: () => activation += "D");

            var modifiedSource = Atom.Value(1);
            var listener = Atom.Computed(() => activationSource.Value + modifiedSource.Value);

            Assert.AreEqual("", activation);

            var autoRun = Atom.AutoRun(() => listener.Get());
            Assert.AreEqual("A", activation);

            modifiedSource.Value = 2;
            Assert.AreEqual("A", activation);

            tick(0);
            Assert.AreEqual("A", activation);

            autoRun.Dispose();
        });

        [Test]
        public void NoReactivationDuringModification() => TestZone.Run(tick =>
        {
            var activation = "";

            var atom = Atom.Value(1,
                onActive: () => activation += "A",
                onInactive: () => activation += "D");

            Assert.AreEqual("", activation);

            var autoRun = Atom.AutoRun(() => atom.Get());
            Assert.AreEqual("A", activation);

            atom.Value = 2;
            Assert.AreEqual("A", activation);

            tick(0);
            Assert.AreEqual("A", activation);

            autoRun.Dispose();
        });

        [Test]
        public void Caching() => TestZone.Run(tick =>
        {
            var random = new Random();
            var atom = Atom.Computed(() => random.Next(), keepAlive: true);

            Assert.AreEqual(atom.Value, atom.Value);
        });

        [Test]
        public void Lazy() => TestZone.Run(tick =>
        {
            var value = 0;
            var atom = Atom.Computed(() => value = 1);

            tick(0f);
            Assert.AreEqual(0, value);

            atom.Get();
            Assert.AreEqual(1, value);
        });

        [Test]
        public void InstantActualization() => TestZone.Run(tick =>
        {
            var source = Atom.Value(1);
            var middle = Atom.Computed(() => source.Value + 1);
            var target = Atom.Computed(() => middle.Value + 1);

            Assert.AreEqual(3, target.Value);

            source.Value = 2;

            Assert.AreEqual(4, target.Value);
        });

        [Test]
        public void DoNotActualizeWhenMastersNotChanged() => TestZone.Run(tick =>
        {
            var targetUpdates = 0;

            var source = Atom.Value(1);
            var middle = Atom.Computed(() => Math.Abs(source.Value));
            var target = Atom.Computed(() =>
            {
                ++targetUpdates;
                return middle.Value;
            }, keepAlive: true);

            target.Get();
            Assert.AreEqual(1, target.Value);

            source.Set(-1);
            target.Get();

            Assert.AreEqual(1, targetUpdates);
        });

        [Test]
        public void ObsoleteAtomsActualizedInInitialOrder() => TestZone.Run(tick =>
        {
            var actualization = "";

            var source = Atom.Value(1);
            var middle = Atom.Computed(() =>
            {
                actualization += "M";
                return source.Value;
            });
            var target = Atom.Computed(() =>
            {
                actualization += "T";
                source.Get();
                return middle.Value;
            });

            var autoRun = Atom.AutoRun(() => target.Get());
            Assert.AreEqual("TM", actualization);

            source.Value = 2;

            tick(0);
            Assert.AreEqual("TMTM", actualization);

            autoRun.Dispose();
        });

        [Test]
        public void AtomicDeferredRestart() => TestZone.Run(tick =>
        {
            int targetValue = 0;

            var source = Atom.Value(1);
            var middle = Atom.Computed(() => source.Value + 1);
            var target = Atom.Computed(() => targetValue = middle.Value + 1, keepAlive: true);

            target.Get();
            Assert.AreEqual(3, targetValue);

            source.Value = 2;
            Assert.AreEqual(3, targetValue);

            tick(0);
            Assert.AreEqual(4, targetValue);
        });

        [Test]
        public void SettingEqualStateAreIgnored() => TestZone.Run(tick =>
        {
            var atom = Atom.Value(new[] {1, 2, 3},
                comparer: new TestComparer<int[]>((a, b) => a.SequenceEqual(b)));

            var v1 = atom.Value;
            var v2 = new[] {1, 2, 3};
            atom.Value = v2;
            var v3 = atom.Value;

            Assert.IsTrue(ReferenceEquals(v1, v3));
            Assert.IsFalse(ReferenceEquals(v2, v3));
        });

        [Test]
        public void ThrowException() => TestZone.Run(tick =>
        {
            var source = Atom.Value(0);
            var exception = new Exception();

            var middle = Atom.Computed(() =>
            {
                if (source.Value == 0)
                    throw exception;

                return source.Value + 1;
            });

            var stack = new Stack<Exception>();

            var reaction = new ReactionAtom(
                reaction: () => middle.Get(),
                exceptionHandler: ex => stack.Push(ex));

            reaction.Get();

            Assert.AreEqual(1, stack.Count);
            Assert.AreEqual(exception, stack.Peek());
            Assert.Throws<Exception>(() => middle.Get());

            source.Value = 1;
            tick(0);

            Assert.AreEqual(2, middle.Value);
        });

        [Test]
        public void Invalidate() => TestZone.Run(tick =>
        {
            //
            var source = Atom.Value(0);

            string actualization = "";

            var dispose = Atom.AutoRun(() =>
            {
                source.Get();
                actualization += "T";
            });

            tick(0);
            Assert.AreEqual("T", actualization);

            source.Invalidate();

            tick(0);
            Assert.AreEqual("TT", actualization);

            dispose.Dispose();
        });

        [Test]
        public void AutoRun() => TestZone.Run(tick =>
        {
            var source = Atom.Value(0);

            int runs = 0;
            var disposer = Atom.AutoRun(() =>
            {
                source.Get();
                ++runs;
            });
            Assert.AreEqual(1, runs);

            source.Value++;
            tick(0);

            Assert.AreEqual(2, runs);

            disposer.Dispose();
            source.Value++;
            tick(0);
            Assert.AreEqual(2, runs);
        });

        [Test]
        public void WhenAtom() => TestZone.Run(tick =>
        {
            var source = Atom.Value(0);

            string watch = "";

            Atom.When(() => source.Value > 1, () => watch += "B");

            tick(0);
            Assert.AreEqual("", watch);

            source.Value = 1;
            tick(0);
            Assert.AreEqual("", watch);

            source.Value = 2;
            tick(0);
            Assert.AreEqual("B", watch);

            source.Value = 3;
            tick(0);
            Assert.AreEqual("B", watch);
        });

        [Test]
        public void Reaction() => TestZone.Run(tick =>
        {
            var source = Atom.Value(0);
            var middle = Atom.Computed(
                () => source.Value < 0 ? throw new Exception() : source.Value);

            var result = 0;
            var errors = 0;

            Atom.Reaction(
                pull: () => middle.Value,
                reaction: (value, disposable) =>
                {
                    result = value;
                    if (value == 2) disposable.Dispose();
                },
                exceptionHandler: ex => ++errors);

            Assert.AreEqual(0, result);

            source.Value = 1;
            tick(0);
            Assert.AreEqual(1, result);

            source.Value = -1;
            tick(0);
            Assert.AreEqual(1, errors);

            source.Value = 2;
            tick(0);
            Assert.AreEqual(2, result);

            source.Value = 3;
            tick(0);
            Assert.AreEqual(2, result);
        });

        [Test]
        public void UnwatchedPullOfObsoleteActiveAtom() => TestZone.Run(tick =>
        {
            var source = Atom.Value(0);

            var computed = Atom.Computed(() => source.Value + 1);
            var computedBase = (AtomBase) computed;

            var reaction = Atom.AutoRun(() => computed.Get());

            Assert.IsTrue(computedBase.IsActive);
            Assert.AreEqual(1, computedBase.Children?.Count ?? 0);

            using (Atom.NoWatch)
            {
                // make source obsolete
                source.Value = 1;
                // pull new value in unwatched scope
                // dependencies must persist
                Assert.AreEqual(2, computed.Value);
            }

            Assert.AreEqual(1, computedBase.Children?.Count ?? 0);

            reaction.Dispose();
        });

        [Test]
        public void UnwatchedCyclicDependency() => TestZone.Run(tick =>
        {
            Atom<int> a, b = null;

            a = Atom.Computed(() => b.Value);
            b = Atom.Computed(() => a.Value);

            Assert.AreEqual(a.SubscribersCount(), 0);
            Assert.AreEqual(b.SubscribersCount(), 0);
            Assert.Throws<CyclicAtomDependencyException>(() => a.Get());
            Assert.Throws<CyclicAtomDependencyException>(() => b.Get());
        });

        [Test]
        public void WatchedCyclicDependency() => TestZone.Run(tick =>
        {
            Atom<int> a, b = null;

            a = Atom.Computed(() => b.Value);
            b = Atom.Computed(() => a.Value);

            Exception exception = null;

            var reaction = Atom.AutoRun(() =>
            {
                a.Get();
                b.Get();
            }, ex => exception = ex);

            Assert.AreEqual(a.SubscribersCount(), 1);
            Assert.AreEqual(b.SubscribersCount(), 1);
            Assert.Throws<CyclicAtomDependencyException>(() => a.Get());
            Assert.Throws<CyclicAtomDependencyException>(() => b.Get());
            Assert.IsTrue(exception is CyclicAtomDependencyException);

            reaction.Dispose();
        });
        
        [Test]
        public void SelfUpdateActualizeNextFrame() => TestZone.Run(tick =>
        {
            var source = Atom.Value(0);

            int runs = 0;
            var reaction = Atom.AutoRun(() =>
            {
                runs++;
                
                if (source.Value < 3)
                {
                    source.Value++;
                }
                else
                {
                    Assert.Fail("Unexpected reaction run. Possible infinite recursion");
                }
            });
            
            Assert.AreEqual(1, runs);
            Assert.AreEqual(1, source.Value);

            tick(0);
            Assert.AreEqual(2, runs);
            Assert.AreEqual(2, source.Value);

            tick(0);
            Assert.AreEqual(3, runs);
            Assert.AreEqual(3, source.Value);
            
            reaction.Dispose();
        });


        class TestComparer<T> : IEqualityComparer<T>
        {
            private readonly Func<T, T, bool> _comparison;

            public TestComparer(Func<T, T, bool> comparison) => _comparison = comparison;

            public bool Equals(T x, T y) => _comparison(x, y);
            public int GetHashCode(T obj) => obj.GetHashCode();
        }
    }
}