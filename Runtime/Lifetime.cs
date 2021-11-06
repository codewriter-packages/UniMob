using System;
using JetBrains.Annotations;
using UnityEngine;

namespace UniMob
{
    public readonly struct Lifetime : IEquatable<Lifetime>
    {
        private readonly ILifetimeController _controller;

        private ILifetimeController Controller => _controller ?? LifetimeController.Eternal;

        internal Lifetime([NotNull] ILifetimeController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        internal void Register(AtomBase atom)
        {
            Controller.Register(atom);
        }

        public void Register(Action action)
        {
            Controller.Register(action);
        }

        public void Register(IDisposable disposable)
        {
            Controller.Register(disposable);
        }

        public bool IsEternal => this == LifetimeController.Eternal.Lifetime;
        public bool IsDisposed => Controller.IsDisposed;

        public static Lifetime Eternal => LifetimeController.Eternal.Lifetime;
        public static Lifetime Terminated => LifetimeController.Terminated.Lifetime;

        public bool Equals(Lifetime other)
        {
            return ReferenceEquals(Controller, other.Controller);
        }

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
    }

    public interface ILifetimeController : IDisposable
    {
        bool IsDisposed { get; }
        Lifetime Lifetime { get; }

        void Register(Action action);
        void Register(AtomBase atom);
        void Register(IDisposable disposable);
    }

    public class LifetimeController : ILifetimeController
    {
        internal static readonly ILifetimeController Eternal = new LifetimeController();
        internal static readonly ILifetimeController Terminated = new LifetimeController();

        internal int registrationCount;
        internal object[] registrations;

        static LifetimeController()
        {
            Terminated.Dispose();
        }

        public bool IsEternal => ReferenceEquals(this, Eternal);

        public bool IsDisposed { get; private set; }

        public Lifetime Lifetime => CreateLifetime(this);

        public void Register([NotNull] Action action)
        {
            RegisterInternal(action);
        }

        public void Register([NotNull] AtomBase atom)
        {
            RegisterInternal(atom);
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

            if (registrationCount == 0)
            {
                return;
            }

            for (var i = registrationCount - 1; i >= 0; i--)
            {
                try
                {
                    switch (registrations[i])
                    {
                        case AtomBase atom:
                            atom.Deactivate();
                            break;

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

        public static Lifetime CreateLifetime([NotNull] LifetimeController controller)
        {
            return new Lifetime(controller);
        }
    }
}