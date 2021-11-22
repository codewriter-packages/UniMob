using NUnit.Framework;
using UniMob.Core;
using Flag = UniMob.Core.AtomOptions;

namespace UniMob.Tests
{
    public class AtomOptionExtensionTests
    {
        [Test]
        public void Has()
        {
            const Flag keys = Flag.Active | Flag.HasCache;
            Assert.IsTrue(keys.Has(Flag.Active));
            Assert.AreEqual(Flag.Active | Flag.HasCache, keys);
        }

        [Test]
        public void HasNot()
        {
            const Flag keys = Flag.Active | Flag.HasCache;
            Assert.IsFalse(keys.Has(Flag.NextDirectEvaluate));
            Assert.AreEqual(Flag.Active | Flag.HasCache, keys);
        }

        [Test]
        public void Set()
        {
            var keys = Flag.HasCache;
            keys.Set(Flag.Active);
            Assert.AreEqual(Flag.Active | Flag.HasCache, keys);
        }

        [Test]
        public void Reset()
        {
            var keys = Flag.Active | Flag.HasCache;
            keys.Reset(Flag.Active);
            Assert.AreEqual(Flag.HasCache, keys);
        }

        [Test]
        public void TryReset_ShouldReset()
        {
            var keys = Flag.Active | Flag.HasCache;
            Assert.IsTrue(keys.TryReset(Flag.Active));
            Assert.AreEqual(Flag.HasCache, keys);
        }

        [Test]
        public void TryReset_ShouldNotReset()
        {
            var keys = Flag.Active | Flag.HasCache;
            Assert.IsFalse(keys.TryReset(Flag.NextDirectEvaluate));
            Assert.AreEqual(Flag.Active | Flag.HasCache, keys);
        }

        [Test]
        public void TrySet_ShouldSet()
        {
            var keys = Flag.HasCache;
            Assert.IsTrue(keys.TrySet(Flag.Active));
            Assert.AreEqual(Flag.HasCache | Flag.Active, keys);
        }

        [Test]
        public void TrySet_ShouldNotSet()
        {
            var keys = Flag.Active | Flag.HasCache;
            Assert.IsFalse(keys.TrySet(Flag.Active));
            Assert.AreEqual(Flag.Active | Flag.HasCache, keys);
        }
    }
}