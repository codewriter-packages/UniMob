using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UniMob.Core;
using UnityEngine;
using UnityEngine.TestTools;
using Random = System.Random;

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

            AtomAssert.That(target).IsNotActive();
        }

        [Test]
        public void ActivationOnUnTrackedRead()
        {
            var target = Atom.Computed(Lifetime, () => 1);

            target.Get();

            AtomAssert.That(target).IsActive();
        }

        [Test]
        public void ActivationOnTrackedRead()
        {
            var target = Atom.Computed(Lifetime, () => 1);

            Atom.Reaction(Lifetime, () => target.Get());

            AtomAssert.That(target).IsActive();
        }

        [Test]
        public void DeactivationOnLifetimeDispose()
        {
            Atom<int> target;

            using (Lifetime.CreateNested(out var nestedLifetime))
            {
                target = Atom.Computed(nestedLifetime, () => 1);
                target.Get();
            }

            AtomAssert.That(target).IsNotActive();
        }

        [Test]
        public void KeepActiveOnUnsubscribe()
        {
            var target = Atom.Computed(Lifetime, () => 1);

            using (Lifetime.CreateNested(out var nestedLifetime))
            {
                Atom.Reaction(nestedLifetime, () => target.Get());
            }

            AtomAssert.That(target).SubscribersCountAreEqualTo(0);
            AtomAssert.That(target).IsActive();
        }

        [Test]
        public void Dispose()
        {
            Atom<int> atom;
            using (Lifetime.CreateNested(out var nestedLifetime))
            {
                atom = Atom.Value(nestedLifetime, 0, debugName: "DisposedAtom");
            }

            atom.Get();
            LogAssert.Expect(LogType.Error, "Actualization of disposed atom (DisposedAtom) can lead to memory leak");

            atom.Deactivate();
        }

        [Test]
        [TestCase("Value")]
        [TestCase("Computed")]
        public void AtomActivatedOnRead(string type)
        {
            var atom = type == "Value" ? Atom.Value(Lifetime, 1) : Atom.Computed(Lifetime, () => 1);
            atom.Get();

            AtomAssert.That(atom).IsActive();
        }

        [Test]
        [TestCase("Value")]
        [TestCase("Computed")]
        public void AtomActivatedOnReadAfterUpdate(string type)
        {
            var atom = type == "Value" ? Atom.Value(Lifetime, 1) : Atom.Computed(Lifetime, () => 1, _ => { });

            atom.Value = 2;
            atom.Get();

            AtomAssert.That(atom).IsActive();
        }

        [Test]
        [TestCase("Value")]
        [TestCase("Computed")]
        public void ValueAtomActivatedOnReadAfterInvalidate(string type)
        {
            var atom = type == "Value" ? Atom.Value(Lifetime, 1) : Atom.Computed(Lifetime, () => 1);

            atom.Invalidate();
            atom.Get();

            AtomAssert.That(atom).IsActive();
        }

        [Test]
        public void Caching()
        {
            var random = new Random();
            var atom = Atom.Computed(Lifetime, () => random.Next());

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
            });

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
            }, keepAlive: true);
            var target = Atom.Computed(Lifetime, () =>
            {
                actualization += "T";
                source.Get();
                return middle.Value;
            }, keepAlive: true);

            target.Get();
            Assert.AreEqual("TM", actualization);

            source.Value = 2;

            AtomScheduler.Sync();
            Assert.AreEqual("TMTM", actualization);
        }

        [Test]
        public void ReactionAutoActualizesOnNextFrame()
        {
            var targetValue = 0;

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
        public void ExceptionHandling()
        {
            var exception = new Exception();
            var stack = new Stack<Exception>();

            var source = Atom.Value(Lifetime, 1);
            var middle = Atom.Computed(Lifetime, () => source.Value == 0 ? throw exception : source.Value + 1);
            Atom.Reaction(Lifetime, () => middle.Get(), ex => stack.Push(ex));

            Assert.AreEqual(2, middle.Value);

            source.Value = 0;
            AtomScheduler.Sync();

            Assert.AreEqual(1, stack.Count);
            Assert.AreEqual(exception, stack.Peek());
            Assert.Throws<Exception>(() => middle.Get());

            source.Value = 3;
            AtomScheduler.Sync();

            Assert.AreEqual(4, middle.Value);
        }

        [Test]
        public void Invalidate()
        {
            var actualization = "";

            var source = Atom.Value(Lifetime, 0);
            Atom.Reaction(Lifetime, () =>
            {
                source.Get();
                actualization += "T";
            });

            AtomScheduler.Sync();
            Assert.AreEqual("T", actualization);

            source.Invalidate();

            AtomScheduler.Sync();
            Assert.AreEqual("TT", actualization);
        }

        [Test]
        public void AutoRun()
        {
            var runs = 0;

            var source = Atom.Value(Lifetime, 0);
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
            var watch = "";

            var source = Atom.Value(Lifetime, 0);
            var reaction = Atom.When(Lifetime, () => source.Value > 1, () => watch += "B");

            AtomScheduler.Sync();
            Assert.AreEqual("", watch);

            source.Value = 1;
            AtomScheduler.Sync();
            Assert.AreEqual("", watch);
            AtomAssert.That(reaction).IsNotDisposed();

            source.Value = 2;
            AtomScheduler.Sync();
            Assert.AreEqual("B", watch);
            AtomAssert.That(reaction).IsDisposed();

            source.Value = 3;
            AtomScheduler.Sync();
            Assert.AreEqual("B", watch);
        }

        [Test]
        public void Reaction()
        {
            var source = Atom.Value(Lifetime, 0);
            var middle = Atom.Computed(Lifetime, () => source.Value < 0 ? throw new Exception() : source.Value);

            var result = 0;
            var errors = 0;

            var nestedDisposer = Lifetime.CreateNested(out var nestedLifetime);

            var reaction = Atom.Reaction(nestedLifetime, () => middle.Value, value =>
            {
                result = value;
                if (value == 2)
                {
                    nestedDisposer.Dispose();
                }
            }, ex => ++errors);

            Assert.AreEqual(0, result);

            source.Value = 1;
            AtomScheduler.Sync();
            Assert.AreEqual(1, result);

            source.Value = -1;
            AtomScheduler.Sync();
            Assert.AreEqual(1, errors);
            AtomAssert.That(reaction).IsNotDisposed();

            source.Value = 2;
            AtomScheduler.Sync();
            Assert.AreEqual(2, result);
            AtomAssert.That(reaction).IsDisposed();

            source.Value = 3;
            AtomScheduler.Sync();
            Assert.AreEqual(2, result);
        }

        [Test]
        public void ReactionUpdatesOnce()
        {
            var watch = "";

            var source = Atom.Value(Lifetime, 0);
            Atom.Reaction(Lifetime, () => source.Value, v => watch += "B");

            Assert.AreEqual("B", watch);

            AtomScheduler.Sync();
            Assert.AreEqual("B", watch);

            source.Value = 1;
            AtomScheduler.Sync();
            Assert.AreEqual("BB", watch);
        }

        [Test]
        public void ReactionWithExceptionUpdatesOnce()
        {
            var watch = "";

            var source = Atom.Value(Lifetime, 0);
            Atom.Reaction<int>(Lifetime, () =>
            {
                source.Get();
                throw new Exception();
            }, v => watch += "B", exceptionHandler: ex => watch += "E");

            Assert.AreEqual("E", watch);

            AtomScheduler.Sync();
            Assert.AreEqual("E", watch);

            source.Value = 1;
            AtomScheduler.Sync();
            Assert.AreEqual("EE", watch);
        }

        [Test]
        public void UnwatchedPullOfObsoleteActiveAtom()
        {
            var source = Atom.Value(Lifetime, 0);
            var computed = Atom.Computed(Lifetime, () => source.Value + 1);

            var reaction = Atom.Reaction(Lifetime, () => computed.Get());

            AtomAssert.That(computed).IsActive();
            AtomAssert.That(computed).ChildrenCountAreEqualTo(1);

            using (Atom.NoWatch)
            {
                // make source obsolete
                source.Value = 1;
                // pull new value in unwatched scope
                // dependencies must persist
                Assert.AreEqual(2, computed.Value);
            }

            AtomAssert.That(computed).ChildrenCountAreEqualTo(1);

            reaction.Deactivate();
        }

        [Test]
        public void UnwatchedCyclicDependency()
        {
            Atom<int> a, b = null;

            a = Atom.Computed(Lifetime, () => b.Value);
            b = Atom.Computed(Lifetime, () => a.Value);

            AtomAssert.That(a).SubscribersCountAreEqualTo(0);
            AtomAssert.That(b).SubscribersCountAreEqualTo(0);
            Assert.Throws<CyclicAtomDependencyException>(() => a.Get());
            Assert.Throws<CyclicAtomDependencyException>(() => b.Get());
        }

        [Test]
        public void WatchedCyclicDependency()
        {
            Atom<int> a, b = null;

            a = Atom.Computed(Lifetime, () => b.Value, debugName: "A");
            b = Atom.Computed(Lifetime, () => a.Value, debugName: "B");

            Exception exception = null;

            var reaction = Atom.Reaction(Lifetime, () =>
            {
                a.Get();
                b.Get();
            }, ex => exception = ex, debugName: "Reaction");

            AtomAssert.That(a).SubscribersCountAreEqualTo(1);
            AtomAssert.That(b).SubscribersCountAreEqualTo(1);

            AtomAssert.That(a).IsSubscribedTo(b);
            AtomAssert.That(reaction).IsSubscribedTo(a);

            Assert.Throws<CyclicAtomDependencyException>(() => a.Get());
            Assert.Throws<CyclicAtomDependencyException>(() => b.Get());
            Assert.IsTrue(exception is CyclicAtomDependencyException);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void RecoverAfterCyclicDependency(bool inverseOrder)
        {
            var s = Atom.Value(Lifetime, false, "SWITCH");

            Atom<int> a, b = null;

            a = Atom.Computed(Lifetime, () => s.Value ? (b.Value + 1) : 0, debugName: "A");
            b = Atom.Computed(Lifetime, () => a.Value + 1, debugName: "B");

            Assert.AreEqual(0, a.Value);
            Assert.AreEqual(1, b.Value);

            s.Value = true;

            if (inverseOrder)
            {
                Assert.Throws<CyclicAtomDependencyException>(() => b.Get());
                Assert.Throws<CyclicAtomDependencyException>(() => a.Get());
            }
            else
            {
                Assert.Throws<CyclicAtomDependencyException>(() => a.Get());
                Assert.Throws<CyclicAtomDependencyException>(() => b.Get());
            }

            s.Value = false;

            Assert.AreEqual(1, b.Value);
            Assert.AreEqual(0, a.Value);
        }

        [Test]
        public void ReactionSelfUpdateActualizeNextFrame()
        {
            var runs = 0;

            var source = Atom.Value(Lifetime, 0, debugName: "source");
            Atom.Reaction(Lifetime, () =>
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
            }, debugName: "reaction");

            Assert.AreEqual(1, runs);
            Assert.AreEqual(1, source.Value);
            LogAssert.Expect(LogType.Error, "Invalidation of atom (source) in watched scope (reaction) is dangerous");

            AtomScheduler.Sync();
            Assert.AreEqual(2, runs);
            Assert.AreEqual(2, source.Value);
            LogAssert.Expect(LogType.Error, "Invalidation of atom (source) in watched scope (reaction) is dangerous");

            AtomScheduler.Sync();
            Assert.AreEqual(3, runs);
            Assert.AreEqual(3, source.Value);
            LogAssert.Expect(LogType.Error, "Invalidation of atom (source) in watched scope (reaction) is dangerous");
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

        [Test]
        public void SubscriptionOnDisposedAtomDoesNotLeadToActualization()
        {
            using (Lifetime.CreateNested(out var nestedLifetime))
            {
                var source = Atom.Value(nestedLifetime, 0);
                Atom.Reaction(Lifetime, () => !nestedLifetime.IsDisposed ? source.Value : 0, v => { });

                AtomScheduler.Sync();
            }

            AtomScheduler.Sync();
        }

        [Test]
        public void SuspendOnInvalidationWithoutKeepAlive()
        {
            var num = 0;
            var atom = Atom.Computed(Lifetime, () => num += 1, keepAlive: false);
            atom.Get();

            Assert.AreEqual(1, num);

            atom.Invalidate();
            AtomScheduler.Sync();

            Assert.AreEqual(1, num);
        }

        [Test]
        public void ActualizeOnInvalidationWithKeepAlive()
        {
            var num = 0;
            var atom = Atom.Computed(Lifetime, () => num += 1, keepAlive: true);
            atom.Get();

            Assert.AreEqual(1, num);

            atom.Invalidate();
            AtomScheduler.Sync();

            Assert.AreEqual(2, num);
        }

        [Test]
        public void SuspendOnDeactivationWithKeepAlive()
        {
            var num = 0;
            var atom = Atom.Computed(Lifetime, () => num += 1, keepAlive: true);
            atom.Get();

            Assert.AreEqual(1, num);

            atom.Deactivate();
            AtomScheduler.Sync();

            Assert.AreEqual(1, num);
        }

        [Test]
        public void InvalidateCurrentScope()
        {
            var atom = Atom.Value(new List<int> {1, 2});
            var sourceWithInvalidation = Atom.Computed(Lifetime, () =>
            {
                Atom.InvalidateCurrentScope();
                return atom.Value;
            });
            var sourceWithRecreation = Atom.Computed(Lifetime, () => atom.Value.ToList());
            var sourceWithoutInvalidation = Atom.Computed(Lifetime, () => atom.Value);

            var resultWithInvalidation = Atom.Computed(Lifetime, () => sourceWithInvalidation.Value.Count);
            var resultWithRecreation = Atom.Computed(Lifetime, () => sourceWithRecreation.Value.Count);
            var resultWithoutInvalidation = Atom.Computed(Lifetime, () => sourceWithoutInvalidation.Value.Count);

            Assert.AreEqual(2, resultWithInvalidation.Value);
            Assert.AreEqual(2, resultWithRecreation.Value);
            Assert.AreEqual(2, resultWithoutInvalidation.Value);

            atom.Value.Add(3);
            atom.Invalidate();
            AtomScheduler.Sync();

            Assert.AreEqual(2 + 1, resultWithInvalidation.Value); // updated due to InvalidateCurrentScope() call
            Assert.AreEqual(2 + 1, resultWithRecreation.Value); // updated due to ToList() call
            Assert.AreEqual(2, resultWithoutInvalidation.Value); // update blocked by EqualityComparer
        }
    }
}