using System;
using System.Threading;
using JetBrains.Annotations;

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
        private readonly LifetimeController _controller;

        private LifetimeController Controller => _controller ?? LifetimeController.Eternal;

        internal Lifetime([NotNull] LifetimeController controller)
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

        internal void UnregisterInternal(object obj)
        {
            Controller.UnregisterInternal(obj);
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
        public LifetimeController CreateNested()
        {
            var nested = new LifetimeController();
            Controller.Register(nested);
            return nested;
        }

        /// <summary>
        /// Create lifetime nested into current lifetime.
        /// </summary>
        /// <param name="lifetime">Nested lifetime.</param>
        /// <returns>Created nested lifetime disposer.</returns>
        public NestedLifetimeDisposer CreateNested(out Lifetime lifetime)
        {
            var nested = new LifetimeController();
            Controller.Register(nested);
            lifetime = nested.Lifetime;
            return new NestedLifetimeDisposer(Controller, nested);
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

    public struct NestedLifetimeDisposer : IDisposable
    {
        private LifetimeController _parent;
        private LifetimeController _child;

        public NestedLifetimeDisposer(LifetimeController parent, LifetimeController child)
        {
            _parent = parent;
            _child = child;
        }

        public void Dispose()
        {
            _parent.UnregisterInternal(_child);
            _child.Dispose();
        }

        /// <summary>
        /// Add action that will be invoked when lifetime disposing start
        /// </summary>
        /// <param name="action">Action to invoke.</param>
        public void Register(Action action)
        {
            _child.Register(action);
        }

        /// <summary>
        /// Add disposable object that will be disposed when lifetime disposing start
        /// </summary>
        /// <param name="disposable">Disposable object</param>
        public void Register(IDisposable disposable)
        {
            _child.Register(disposable);
        }
    }
}