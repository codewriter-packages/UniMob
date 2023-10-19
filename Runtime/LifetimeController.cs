using System;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using UniMob.Core;
using UnityEngine;
using UnityEngine.Assertions;

namespace UniMob
{
    public class LifetimeController : ILifetimeController
    {
        internal static readonly Stack<LifetimeController> Pool = new Stack<LifetimeController>();

        internal static readonly LifetimeController Eternal = new LifetimeController();
        internal static readonly LifetimeController Terminated = new LifetimeController();

        internal int registrationCount;
        internal object[] registrations;

        internal CancellationTokenSource cancellationTokenSource;

        internal bool hasEmptySlots;

        static LifetimeController()
        {
            Terminated.ToCancellationToken();
            Terminated.Dispose();
        }

        public bool IsEternal => ReferenceEquals(this, Eternal);

        public bool IsDisposed { get; private set; }

        public Lifetime Lifetime => CreateLifetime(this);

        public void Register([NotNull] Action action)
        {
            RegisterInternal(action);
        }

        public void Register([NotNull] IDisposable disposable)
        {
            RegisterInternal(disposable);
        }

        internal void Setup()
        {
            Assert.IsFalse(IsEternal);
            Assert.IsTrue(IsDisposed);
            Assert.IsFalse(hasEmptySlots);
            Assert.IsNull(registrations);
            Assert.AreEqual(0, registrationCount);
            Assert.IsNull(cancellationTokenSource);

            IsDisposed = false;
        }

        public void Dispose()
        {
            if (IsEternal)
            {
                throw new InvalidOperationException("Cannot dispose eternal lifetime controller");
            }

            if (IsDisposed)
            {
                return;
            }
            
            if (registrationCount > 0)
            {
                for (var i = registrationCount - 1; i >= 0; i--)
                {
                    try
                    {
                        switch (registrations[i])
                        {
                            case IDisposable disposable:
                                disposable.Dispose();
                                break;

                            case Action action:
                                action.Invoke();
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }

                    registrations[i] = null;
                }

                registrationCount = 0;
                ArrayPool<object>.Return(ref registrations);
            }
            
            IsDisposed = true;
            hasEmptySlots = false;
            cancellationTokenSource?.Cancel();

            if (!ReferenceEquals(this, Terminated))
            {
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
            }
        }

        public CancellationToken ToCancellationToken()
        {
            return (cancellationTokenSource ?? CreateCtsLazily()).Token;
        }

        private void RegisterInternal(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            if (IsDisposed)
            {
                throw new ObjectDisposedException("Cannot Register on disposed Lifetime");
            }

            if (IsEternal)
            {
                return;
            }

            if (registrations == null)
            {
                ArrayPool<object>.Rent(out registrations, 2);
            }
            else if (registrationCount == registrations.Length)
            {
                if (hasEmptySlots)
                {
                    CompressEmptySlots();
                }
                else
                {
                    ArrayPool<object>.Grow(ref registrations);
                }
            }

            registrations[registrationCount++] = obj;
        }

        internal void UnregisterInternal(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            if (IsDisposed)
            {
                throw new ObjectDisposedException("Cannot Register on disposed Lifetime");
            }

            if (IsEternal)
            {
                return;
            }

            for (var i = 0; i < registrationCount; i++)
            {
                if (registrations[i] != obj)
                {
                    continue;
                }

                registrations[i] = null;
                hasEmptySlots = true;
                break;
            }
        }

        private void CompressEmptySlots()
        {
            hasEmptySlots = false;

            var newRegistrationCount = 0;
            for (var i = 0; i < registrationCount; i++)
            {
                if (registrations[i] != null)
                {
                    registrations[newRegistrationCount++] = registrations[i];
                }
            }

            for (var i = newRegistrationCount; i < registrationCount; i++)
            {
                registrations[i] = null;
            }

            registrationCount = newRegistrationCount;
        }

        private CancellationTokenSource CreateCtsLazily()
        {
            if (cancellationTokenSource != null)
            {
                return cancellationTokenSource;
            }

            cancellationTokenSource = new CancellationTokenSource();

            if (IsDisposed)
            {
                cancellationTokenSource.Cancel();
            }

            return cancellationTokenSource;
        }

        public static Lifetime CreateLifetime([NotNull] LifetimeController controller)
        {
            return new Lifetime(controller);
        }
    }
}