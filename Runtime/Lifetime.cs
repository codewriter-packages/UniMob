using System;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;

namespace UniMob
{
    /// <summary>
    /// Lifetime has two main functions:<br/>
    /// 1. High performance analogue of <see cref="CancellationToken"/>. <see cref="LifetimeController"/> is analogue of <see cref="CancellationTokenSource"/> <br/>
    /// 2. Inversion of <see cref="IDisposable"/> pattern:
    /// user can add disposable resources into Lifetime with bunch of <c>Register</c> (e.g. <see cref="Register(Action)"/>) methods.
    /// When lifetime is being disposed all previously added disposable resources are being disposed in stack-way LIFO order.
    /// </summary>
    public readonly struct Lifetime : IEquatable<Lifetime>
    {
        private readonly ILifetimeController _controller;

        private ILifetimeController Controller => _controller ?? LifetimeController.Eternal;

        internal Lifetime([NotNull] ILifetimeController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        /// <summary>
        /// Add action that will be invoked when lifetime disposing start
        /// </summary>
        /// <param name="action">Action to invoke.</param>
        public void Register(Action action)
        {
            Controller.Register(action);
        }

        /// <summary>
        /// Add disposable object that will be disposed when lifetime disposing start
        /// </summary>
        /// <param name="disposable">Disposable object</param>
        public void Register(IDisposable disposable)
        {
            Controller.Register(disposable);
        }

        /// <summary>
        /// Whether current lifetime is equal to <see cref="Eternal"/> and never be disposed
        /// </summary>
        public bool IsEternal => this == LifetimeController.Eternal.Lifetime;

        /// <summary>
        /// Whether current lifetime is disposed
        /// </summary>
        public bool IsDisposed => Controller.IsDisposed;

        /// <summary>
        /// A lifetime that never ends. Scheduling actions on such a lifetime has no effect.
        /// </summary>
        public static Lifetime Eternal => LifetimeController.Eternal.Lifetime;

        /// <summary>
        /// Singleton lifetime that disposed by default.  
        /// </summary>
        public static Lifetime Terminated => LifetimeController.Terminated.Lifetime;

        public bool Equals(Lifetime other)
        {
            return ReferenceEquals(Controller, other.Controller);
        }

        /// <summary>
        /// Create lifetime controller nested into current lifetime.
        /// </summary>
        /// <returns>Created nested lifetime controller.</returns>
        public ILifetimeController CreateNested()
        {
            var nested = new LifetimeController();
            Controller.Register(nested);
            return nested;
        }

        public override bool Equals(object obj)
        {
            return obj is Lifetime other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Controller.GetHashCode();
        }

        public static bool operator ==(Lifetime a, Lifetime b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Lifetime a, Lifetime b)
        {
            return !(a == b);
        }

        public static implicit operator CancellationToken(Lifetime lifetime)
        {
            return lifetime.Controller.ToCancellationToken();
        }
    }

    public interface ILifetimeController : IDisposable
    {
        bool IsDisposed { get; }
        Lifetime Lifetime { get; }

        void Register(Action action);
        void Register(IDisposable disposable);

        CancellationToken ToCancellationToken();
    }

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

    public interface ILifetimeScope
    {
        Lifetime Lifetime { get; }
    }
}