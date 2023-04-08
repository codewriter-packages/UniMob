using System;
using NUnit.Framework;
using UniMob.Core;

namespace UniMob.Tests
{
    public class AtomWeaverTests
    {
        [Test]
        public void WeavedValueRead()
        {
            using (var lc = new LifetimeController())
            {
                var c = new TestClass(lc.Lifetime);

                Assert.AreEqual(1, c.Value);

                lc.Dispose();

                Assert.Throws<ObjectDisposedException>(() => c.GetValue());
            }
        }

        [Test]
        public void Pooling()
        {
            CodeGenAtom.Pool<int>.Clear();

            using (var lc = new LifetimeController())
            {
                var c = new TestClass(lc.Lifetime);

                Assert.AreEqual(0, CodeGenAtom.Pool<int>.Atoms.Count);
                Assert.AreEqual(0, CodeGenAtom.Pool<int>.Recyclers.Count);

                c.GetValue();

                Assert.AreEqual(0, CodeGenAtom.Pool<int>.Atoms.Count);
                Assert.AreEqual(0, CodeGenAtom.Pool<int>.Recyclers.Count);

                lc.Dispose();

                Assert.AreEqual(1, CodeGenAtom.Pool<int>.Atoms.Count);
                Assert.AreEqual(1, CodeGenAtom.Pool<int>.Recyclers.Count);
            }

            using (var lc2 = new LifetimeController())
            {
                var c2 = new TestClass(lc2.Lifetime);

                Assert.AreEqual(1, CodeGenAtom.Pool<int>.Atoms.Count);
                Assert.AreEqual(1, CodeGenAtom.Pool<int>.Recyclers.Count);

                c2.GetValue();

                Assert.AreEqual(0, CodeGenAtom.Pool<int>.Atoms.Count);
                Assert.AreEqual(0, CodeGenAtom.Pool<int>.Recyclers.Count);
            }
        }

        [AtomContainer]
        private class TestClass : ILifetimeScope
        {
            public TestClass(Lifetime lifetime)
            {
                Lifetime = lifetime;
            }

            public Lifetime Lifetime { get; }

            [Atom] public int Value => 1;

            public int GetValue() => Value;
        }
    }
}