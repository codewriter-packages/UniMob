using System;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;

namespace UniMob
{
    public class LifetimeController : ILifetimeController
    {
        internal static readonly ILifetimeController Eternal = new LifetimeController();
        internal static readonly ILifetimeController Terminated = new LifetimeController();

        internal int registrationCount;
        internal object[] registrations;

        internal CancellationTokenSource cancellationTokenSource;

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

            IsDisposed = true;

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
            registrations = null;

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
                registrations = new object[1];
            }

            if (registrationCount == registrations.Length)
            {
                var newRegistrationCount = 0;
                for (var i = 0; i < registrationCount; i++)
                {
                    if (registrations[i] is ILifetimeController lc && lc.IsDisposed)
                    {
                        registrations[i] = null;
                    }
                    else
                    {
                        registrations[newRegistrationCount++] = registrations[i];
                    }
                }

                for (var i = newRegistrationCount; i < registrationCount; i++)
                {
                    registrations[i] = null;
                }

                registrationCount = newRegistrationCount;

                if (newRegistrationCount * 2 > registrations.Length)
                {
                    Array.Resize(ref registrations, newRegistrationCount * 2);
                }
            }

            registrations[registrationCount++] = obj;
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