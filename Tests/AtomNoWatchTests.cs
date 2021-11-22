using NUnit.Framework;

namespace UniMob.Tests
{
    public class AtomNoWatchTests
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
        public void NoWatch()
        {
            Reaction reaction = null;
            reaction = Atom.Reaction(Lifetime, () =>
            {
                // ReSharper disable once AccessToModifiedClosure
                AtomAssert.CurrentScopeIs(reaction);

                using (Atom.NoWatch)
                {
                    AtomAssert.CurrentScopeIsNull();

                    using (Atom.NoWatch)
                    {
                        AtomAssert.CurrentScopeIsNull();
                    }

                    AtomAssert.CurrentScopeIsNull();
                }

                // ReSharper disable once AccessToModifiedClosure
                AtomAssert.CurrentScopeIs(reaction);
            });
        }
    }
}