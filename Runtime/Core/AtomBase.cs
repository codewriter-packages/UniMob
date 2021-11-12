using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace UniMob.Core
{
    public abstract class AtomBase : IEquatable<AtomBase>, IDisposable
    {
        private readonly string _debugName;
        private List<AtomBase> _children;
        private List<AtomBase> _subscribers;

        internal AtomOptions options;
        internal AtomState state = AtomState.Obsolete;

        [CanBeNull] public IReadOnlyList<AtomBase> Children => _children;
        [CanBeNull] public IReadOnlyList<AtomBase> Subscribers => _subscribers;

        public int SubscribersCount => _subscribers?.Count ?? 0;
        public string DebugName => _debugName;
        public bool IsActive => options.Has(AtomOptions.Active);
        public bool IsDisposed => options.Has(AtomOptions.Disposed);
        public AtomState State => state;

        public enum AtomState : byte
        {
            Obsolete,
            Checking,
            Pulling,
            Actual,
        }

        [Flags]
        public enum AtomOptions : byte
        {
            None = 0,
            AutoActualize = 1 << 0,
            Active = 1 << 1,
            Disposed = 1 << 2,

            HasCache = 1 << 6,
            NextDirectEvaluate = 1 << 7,
        }

        internal AtomBase(Lifetime lifetime, string debugName, AtomOptions options)
        {
            _debugName = debugName;
            this.options = options;

            lifetime.Register(this);
        }

        public bool Equals(AtomBase other)
        {
            return ReferenceEquals(this, other);
        }

        void IDisposable.Dispose()
        {
            Deactivate();
        }

        public virtual void Deactivate()
        {
            if (_children != null)
            {
                for (var i = 0; i < _children.Count; i++)
                {
                    _children[i].RemoveSubscriber(this);
                }

                DeleteList(ref _children);
            }

            if (_subscribers != null)
            {
                for (var i = 0; i < _subscribers.Count; i++)
                {
                    _subscribers[i].Check();
                }
            }

            if (options.Has(AtomOptions.Active))
            {
                options.Reset(AtomOptions.Active);
                AtomRegistry.OnInactivate(this);
            }

            state = AtomState.Obsolete;
        }

        public void Actualize(bool force = false)
        {
            if (state == AtomState.Pulling)
            {
                throw new CyclicAtomDependencyException(this);
            }

            if (!force && state == AtomState.Actual)
            {
                return;
            }

            Stack.Push(this);

            if (!options.Has(AtomOptions.Active))
            {
                options.Set(AtomOptions.Active);
                AtomRegistry.OnActivate(this);
            }

            if (!force && state == AtomState.Checking)
            {
                for (var i = 0; i < _children.Count; i++)
                {
                    if (state != AtomState.Checking)
                    {
                        break;
                    }

                    _children[i].Actualize();
                }

                if (state == AtomState.Checking)
                {
                    state = AtomState.Actual;
                }
            }

            if (force || state != AtomState.Actual)
            {
                var oldChildren = _children;
                if (oldChildren != null)
                {
                    _children = null;

                    for (var i = 0; i < oldChildren.Count; i++)
                    {
                        oldChildren[i].RemoveSubscriber(this);
                    }

                    DeleteList(ref oldChildren);
                }

                state = AtomState.Pulling;

                Evaluate();
            }

            Stack.Pop();
        }

        protected abstract void Evaluate();

        protected void ObsoleteSubscribers()
        {
            if (_subscribers == null)
            {
                return;
            }

            for (var i = 0; i < _subscribers.Count; i++)
            {
                _subscribers[i].Obsolete();
            }
        }

        private void CheckSubscribers()
        {
            if (_subscribers != null)
            {
                for (var i = 0; i < _subscribers.Count; i++)
                {
                    _subscribers[i].Check();
                }
            }
            else if (options.Has(AtomOptions.AutoActualize))
            {
                AtomScheduler.Actualize(this);
            }
        }

        private void Check()
        {
            if (state == AtomState.Actual || state == AtomState.Pulling)
            {
                state = AtomState.Checking;
                CheckSubscribers();
            }
        }

        private void Obsolete()
        {
            if (state == AtomState.Obsolete)
            {
                return;
            }

            state = AtomState.Obsolete;
            CheckSubscribers();
        }

        protected void AddSubscriber(AtomBase subscriber)
        {
            if (_subscribers == null)
            {
                CreateList(out _subscribers);
            }

            _subscribers.Add(subscriber);
        }

        private void RemoveSubscriber(AtomBase subscriber)
        {
            if (_subscribers == null)
            {
                return;
            }

            _subscribers.Remove(subscriber);

            if (_subscribers.Count == 0)
            {
                DeleteList(ref _subscribers);
            }
        }

        internal void AddChildren(AtomBase child)
        {
            if (_children == null)
            {
                CreateList(out _children);
            }

            _children.Add(child);
        }

        protected void SubscribeToParent()
        {
            var parent = Stack.Peek();
            if (parent != null)
            {
                AddSubscriber(parent);
                parent.AddChildren(this);
            }
        }

        public override string ToString()
        {
            return _debugName ?? "[Anonymous]";
        }

        [NotNull] internal static readonly Stack<AtomBase> Stack = new Stack<AtomBase>();

        static AtomBase()
        {
            Stack.Push(null);
        }

        private static readonly Stack<List<AtomBase>> ListPool = new Stack<List<AtomBase>>();

        private static void CreateList(out List<AtomBase> list)
        {
            list = ListPool.Count > 0 ? ListPool.Pop() : new List<AtomBase>();
        }

        private static void DeleteList(ref List<AtomBase> list)
        {
            list.Clear();
            ListPool.Push(list);
            list = null;
        }
    }

    internal static class AtomOptionExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this AtomBase.AtomOptions keys, in AtomBase.AtomOptions flag)
        {
            return (keys & flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Set(this ref AtomBase.AtomOptions keys, in AtomBase.AtomOptions flag)
        {
            keys |= flag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Reset(this ref AtomBase.AtomOptions keys, in AtomBase.AtomOptions flag)
        {
            keys &= ~flag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReset(this ref AtomBase.AtomOptions keys, in AtomBase.AtomOptions flag)
        {
            return keys != (keys &= ~flag);
        }
    }
}