using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace UniMob.Tests
{
    public class AtomTests
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
            var target = Atom.Computed(Lifetime, () => 1);

            Assert.IsFalse(target.IsActive());
        }

        [Test]
        public void ActivationOnUnTrackedRead()
        {
            var target = Atom.Computed(Lifetime, () => 1);

            target.Get();

            Assert.IsTrue(target.IsActive());
        }

        [Test]
        public void ActivationOnTrackedRead()
        {
            var target = Atom.Computed(Lifetime, () => 1);

            Atom.Reaction(Lifetime, () => target.Get());

            Assert.IsTrue(target.IsActive());
        }

        [Test]
        public void DeactivationOnLifetimeDispose()
        {
            Atom<int> target;

            using (var nested = Lifetime.CreateNested())
            {
                target = Atom.Computed(nested.Lifetime, () => 1);
                target.Get();
            }

            Assert.IsFalse(target.IsActive());
        }

        [Test]
        public void KeepActiveOnUnsubscribe()
        {
            var target = Atom.Computed(Lifetime, () => 1);

            using (var nested = Lifetime.CreateNested())
            {
                Atom.Reaction(nested.Lifetime, () => target.Get());
            }

            Assert.IsTrue(target.IsActive());
        }

        [Test]
        public void Caching()
        {
            var random = new Random();
            var atom = Atom.Computed(Lifetime, () => random.Next() /*, keepAlive: true*/);

            Assert.AreEqual(atom.Value, atom.Value);
        }

        [Test]
        public void Lazy()
        {
            var value = 0;
            var atom = Atom.Computed(Lifetime, () => value = 1);

            AtomScheduler.Sync();
            Assert.AreEqual(0, value);

            atom.Get();
            Assert.AreEqual(1, value);
        }

        [Test]
        public void InstantActualization()
        {
            var source = Atom.Value(Lifetime, 1);
            var middle = Atom.Computed(Lifetime, () => source.Value + 1);
            var target = Atom.Computed(Lifetime, () => middle.Value + 1);

            Assert.AreEqual(3, target.Value);

            source.Value = 2;

            Assert.AreEqual(4, target.Value);
        }

        [Test]
        public void DoNotActualizeWhenMastersNotChanged()
        {
            var targetUpdates = 0;

            var source = Atom.Value(Lifetime, 1);
            var middle = Atom.Computed(Lifetime, () => Math.Abs(source.Value));
            var target = Atom.Computed(Lifetime, () =>
            {
                ++targetUpdates;
                return middle.Value;
            } /*, keepAlive: true*/);

            target.Get();
            Assert.AreEqual(1, target.Value);

            source.Set(-1);
            target.Get();

            Assert.AreEqual(1, targetUpdates);
        }

        [Test]
        public void ObsoleteAtomsActualizedInInitialOrder()
        {
            var actualization = "";

            var source = Atom.Value(Lifetime, 1);
            var middle = Atom.Computed(Lifetime, () =>
            {
                actualization += "M";
                return source.Value;
            });
            var target = Atom.Computed(Lifetime, () =>
            {
                actualization += "T";
                source.Get();
                return middle.Value;
            });

            var autoRun = Atom.Reaction(Lifetime, () => target.Get());
            Assert.AreEqual("TM", actualization);

            source.Value = 2;

            AtomScheduler.Sync();
            Assert.AreEqual("TMTM", actualization);

            autoRun.Deactivate();
        }


        [Test]
        public void ReactionAutoActualizes()
        {
            int targetValue = 0;

            var source = Atom.Value(Lifetime, 1);
            var middle = Atom.Computed(Lifetime, () => source.Value + 1);
            Atom.Reaction(Lifetime, () => targetValue = middle.Value + 1);

            Assert.AreEqual(3, targetValue);

            source.Value = 2;
            Assert.AreEqual(3, targetValue);

            AtomScheduler.Sync();
            Assert.AreEqual(4, targetValue);
        }

        [Test]
        public void ComputedNotActualizesWithoutDirectAccess()
        {
            int targetValue = 0;

            var source = Atom.Value(Lifetime, 1);
            var middle = Atom.Computed(Lifetime, () => source.Value + 1);
            var target = Atom.Computed(Lifetime, () => targetValue = middle.Value + 1);

            target.Get();
            Assert.AreEqual(3, targetValue);

            source.Value = 2;
            Assert.AreEqual(3, targetValue);

            AtomScheduler.Sync();
            Assert.AreEqual(3, targetValue);

            target.Get();
            Assert.AreEqual(4, targetValue);
        }

        [Test]
        public void SettingEqualStateAreIgnored()
        {
            var runs = 0;

            var source = Atom.Value(Lifetime, 1);
            Atom.Reaction(Lifetime, () =>
            {
                runs++;
                source.Get();
            });

            AtomScheduler.Sync();
            Assert.AreEqual(1, runs);

            source.Value = 1;

            AtomScheduler.Sync();
            Assert.AreEqual(1, runs);
        }

        [Test]
        public void ThrowException()
        {
            var source = Atom.Value(Lifetime, 0);
            var exception = new Exception();

            var middle = Atom.Computed(Lifetime, () =>
            {
                if (source.Value == 0)
                    throw exception;

                return source.Value + 1;
            });

            var stack = new Stack<Exception>();

            var reaction = new ReactionAtom(
                Lifetime,
                debugName: null,
                reaction: () => middle.Get(),
                exceptionHandler: ex => stack.Push(ex));

            reaction.Activate();

            Assert.AreEqual(1, stack.Count);
            Assert.AreEqual(exception, stack.Peek());
            Assert.Throws<Exception>(() => middle.Get());

            source.Value = 1;
            AtomScheduler.Sync();

            Assert.AreEqual(2, middle.Value);
        }

        [Test]
        public void Invalidate()
        {
            //
            var source = Atom.Value(Lifetime, 0);

            string actualization = "";

            var dispose = Atom.Reaction(Lifetime, () =>
            {
                source.Get();
                actualization += "T";
            });

            AtomScheduler.Sync();
            Assert.AreEqual("T", actualization);

            source.Invalidate();

            AtomScheduler.Sync();
            Assert.AreEqual("TT", actualization);

            dispose.Deactivate();
        }

        [Test]
        public void AutoRun()
        {
            var source = Atom.Value(Lifetime, 0);

            int runs = 0;
            var disposer = Atom.Reaction(Lifetime, () =>
            {
                source.Get();
                ++runs;
            });
            Assert.AreEqual(1, runs);

            source.Value++;
            AtomScheduler.Sync();

            Assert.AreEqual(2, runs);

            disposer.Deactivate();
            source.Value++;
            AtomScheduler.Sync();
            Assert.AreEqual(2, runs);
        }

        [Test]
        public void WhenAtom()
        {
            var source = Atom.Value(Lifetime, 0);

            string watch = "";

            Atom.When(Lifetime, () => source.Value > 1, () => watch += "B");

            AtomScheduler.Sync();
            Assert.AreEqual("", watch);

            source.Value = 1;
            AtomScheduler.Sync();
            Assert.AreEqual("", watch);

            source.Value = 2;
            AtomScheduler.Sync();
            Assert.AreEqual("B", watch);

            source.Value = 3;
            AtomScheduler.Sync();
            Assert.AreEqual("B", watch);
        }

        [Test]
        public void Reaction()
        {
            var source = Atom.Value(Lifetime, 0);
            var middle = Atom.Computed(Lifetime,
                () => source.Value < 0 ? throw new Exception() : source.Value);

            var result = 0;
            var errors = 0;

            Atom.Reaction(
                Lifetime,
                reaction: () => middle.Value,
                effect: (value, disposable) =>
                {
                    result = value;
                    if (value == 2) disposable.Deactivate();
                },
                exceptionHandler: ex => ++errors);

            Assert.AreEqual(0, result);

            source.Value = 1;
            AtomScheduler.Sync();
            Assert.AreEqual(1, result);

            source.Value = -1;
            AtomScheduler.Sync();
            Assert.AreEqual(1, errors);

            source.Value = 2;
            AtomScheduler.Sync();
            Assert.AreEqual(2, result);

            source.Value = 3;
            AtomScheduler.Sync();
            Assert.AreEqual(2, result);
        }

        [Test]
        public void ReactionUpdatesOnce()
        {
            var source = Atom.Value(Lifetime, 0);

            var watch = "";
            var reaction = Atom.Reaction(Lifetime, () => source.Value, v => watch += "B");

            Assert.AreEqual("B", watch);

            AtomScheduler.Sync();
            Assert.AreEqual("B", watch);

            reaction.Deactivate();
        }

        [Test]
        public void UnwatchedPullOfObsoleteActiveAtom()
        {
            var source = Atom.Value(Lifetime, 0);

            var computed = Atom.Computed(Lifetime, () => source.Value + 1);
            var computedBase = (AtomBase) computed;

            var reaction = Atom.Reaction(Lifetime, () => computed.Get());

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

            reaction.Deactivate();
        }

        [Test]
        public void UnwatchedCyclicDependency()
        {
            Atom<int> a, b = null;

            a = Atom.Computed(Lifetime, () => b.Value);
            b = Atom.Computed(Lifetime, () => a.Value);

            Assert.AreEqual(a.SubscribersCount(), 0);
            Assert.AreEqual(b.SubscribersCount(), 0);
            Assert.Throws<CyclicAtomDependencyException>(() => a.Get());
            Assert.Throws<CyclicAtomDependencyException>(() => b.Get());
        }

        [Test]
        public void WatchedCyclicDependency()
        {
            Atom<int> a, b = null;

            a = Atom.Computed(Lifetime, () => b.Value);
            b = Atom.Computed(Lifetime, () => a.Value);

            Exception exception = null;

            var reaction = Atom.Reaction(Lifetime, () =>
            {
                a.Get();
                b.Get();
            }, ex => exception = ex);

            Assert.AreEqual(a.SubscribersCount(), 1);
            Assert.AreEqual(b.SubscribersCount(), 1);
            Assert.Throws<CyclicAtomDependencyException>(() => a.Get());
            Assert.Throws<CyclicAtomDependencyException>(() => b.Get());
            Assert.IsTrue(exception is CyclicAtomDependencyException);

            reaction.Deactivate();
        }

        [Test]
        public void SelfUpdateActualizeNextFrame()
        {
            var source = Atom.Value(Lifetime, 0);

            int runs = 0;
            var reaction = Atom.Reaction(Lifetime, () =>
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

            AtomScheduler.Sync();
            Assert.AreEqual(2, runs);
            Assert.AreEqual(2, source.Value);

            AtomScheduler.Sync();
            Assert.AreEqual(3, runs);
            Assert.AreEqual(3, source.Value);

            reaction.Deactivate();
        }

        [Test]
        public void MutableAtomPushNewValue()
        {
            var source = 0;

            var medium = Atom.Computed(
                Lifetime,
                () => source,
                val => source = val
            );

            Assert.AreEqual(0, source);
            Assert.AreEqual(0, medium.Value);

            medium.Value = 1;

            Assert.AreEqual(1, source);
            Assert.AreEqual(1, medium.Value);
        }
    }
}