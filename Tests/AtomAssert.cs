using JetBrains.Annotations;
using NUnit.Framework;
using UniMob.Core;

namespace UniMob.Tests
{
    public static class AtomAssert
    {
        public static Builder That<T>(Atom<T> atom) => new Builder((AtomBase) atom);

        public static Builder That(Reaction atom) => new Builder((AtomBase) atom);

        [AssertionMethod]
        public static void CurrentScopeIsNull()
        {
            if (Atom.CurrentScope != null)
            {
                Assert.Fail($"Expected null atom scope but found '{AtomBase.Stack}'");
            }
        }

        [AssertionMethod]
        public static void CurrentScopeIsNotNull()
        {
            if (Atom.CurrentScope == null)
            {
                Assert.Fail($"Expected not null atom scope but scope not found");
            }
        }

        [AssertionMethod]
        public static void CurrentScopeIs(Reaction atom)
        {
            if (Atom.CurrentScope == null)
            {
                Assert.Fail($"Expected '{atom}' atom scope but scope is null");
            }
        }

        public readonly struct Builder
        {
            private readonly AtomBase _atom;

            public Builder(AtomBase atom) => _atom = atom;

            [AssertionMethod]
            public void IsActive()
            {
                if (!_atom.options.Has(AtomOptions.Active))
                {
                    Assert.Fail($"Atom '{_atom}' not active");
                }
            }

            [AssertionMethod]
            public void IsNotActive()
            {
                if (_atom.options.Has(AtomOptions.Active))
                {
                    Assert.Fail($"Atom '{_atom}' active");
                }
            }

            [AssertionMethod]
            public void StateIs(AtomState state)
            {
                if (_atom.state != state)
                {
                    Assert.Fail($"Atom '{_atom}' state is {_atom.state} children but expected {state}");
                }
            }

            [AssertionMethod]
            public void ChildrenCountAreEqualTo(int count)
            {
                if (_atom.childrenCount != count)
                {
                    Assert.Fail($"Atom '{_atom}' has {_atom.childrenCount} children but expected {count}");
                }
            }

            [AssertionMethod]
            public void SubscribersCountAreEqualTo(int count)
            {
                if (_atom.subscribersCount != count)
                {
                    Assert.Fail($"Atom '{_atom}' has {_atom.subscribersCount} subscribers but expected {count}");
                }
            }

            [AssertionMethod]
            public void IsSubscribedTo<T>(Atom<T> source)
            {
                var sourceAtom = (AtomBase) source;

                if (!TryFind(_atom.children, _atom.childrenCount, sourceAtom))
                {
                    Assert.Fail($"Atom '{_atom}' not subscribed to '{source}' (children)");
                }

                if (!TryFind(sourceAtom.subscribers, sourceAtom.subscribersCount, _atom))
                {
                    Assert.Fail($"Atom '{_atom}' subscribed to '{source}' (subscribers)");
                }
            }

            [AssertionMethod]
            public void IsNotSubscribedTo<T>(Atom<T> source)
            {
                var sourceAtom = (AtomBase) source;

                if (TryFind(_atom.children, _atom.childrenCount, sourceAtom))
                {
                    Assert.Fail($"Atom '{_atom}' subscribed to '{source}' (children)");
                }

                if (TryFind(sourceAtom.subscribers, sourceAtom.subscribersCount, _atom))
                {
                    Assert.Fail($"Atom '{_atom}' subscribed to '{source}' (subscribers)");
                }
            }

            private static bool TryFind(AtomBase[] array, int count, AtomBase target)
            {
                for (int i = 0; i < count; i++)
                {
                    if (array[i] == target)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}