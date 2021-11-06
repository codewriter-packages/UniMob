using System.Threading;
using NUnit.Framework;

namespace UniMob.Tests
{
    public class AtomWhenTests
    {
        /*
        private AtomLifetimeController _lifetimeController;
        private AtomLifetime _lifetime;

        [SetUp]
        public void SetUp()
        {
            _lifetimeController = new AtomLifetimeController();
            _lifetime = _lifetimeController.Lifetime;
        }

        [TearDown]
        public void TearDown()
        {
            _lifetimeController.Dispose();
        }
        
        [Test]
        public void NormalComplete_DefaultToken()
        {
            NormalComplete(default);
        }

        [Test]
        public void NormalComplete_CustomToken()
        {
            var cts = new CancellationTokenSource();
            NormalComplete(cts.Token);
            cts.Dispose();
        }
        
        public void NormalComplete(CancellationToken cancellationToken)
        {
            var atom = Atom.Value(_lifetime,0);
            var runs = 0;

            var task = Atom.When(_lifetime, () =>
            {
                runs++;
                return atom.Value == 1;
            }, cancellationToken);

            Assert.AreEqual(1, runs);
            Assert.IsFalse(task.IsCompleted);

            atom.Value = 1;
            AtomScheduler.Sync();

            Assert.AreEqual(2, runs);
            Assert.IsTrue(task.IsCompleted);
        }

        [Test]
        public void CreateWithCancelledToken()
        {
            var atom = Atom.Value(0);
            var runs = 0;
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var task = Atom.When(() =>
            {
                runs++;
                return atom.Value == 1;
            }, cts.Token);

            Assert.AreEqual(0, runs);
            Assert.IsTrue(task.IsCanceled);

            cts.Dispose();
        }

        [Test]
        public void CancelToken()
        {
            var atom = Atom.Value(0);
            var runs = 0;
            var cts = new CancellationTokenSource();

            var task = Atom.When(() =>
            {
                runs++;
                return atom.Value == 1;
            }, cts.Token);

            Assert.AreEqual(1, runs);
            Assert.IsFalse(task.IsCompleted);
            Assert.IsFalse(task.IsCanceled);

            cts.Cancel();

            Assert.AreEqual(1, runs);
            Assert.IsTrue(task.IsCompleted);
            Assert.IsTrue(task.IsCanceled);

            atom.Value = 0;

            Assert.AreEqual(1, runs);

            cts.Dispose();
        }
        */
    }
}