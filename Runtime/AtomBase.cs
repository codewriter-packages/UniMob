using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace UniMob
{
    public abstract class AtomBase : IEquatable<AtomBase>
    {
        private readonly string _debugName;
        private List<AtomBase> _children;
        private List<AtomBase> _subscribers;
        public AtomOptions options;

        internal AtomState State = AtomState.Obsolete;

        [CanBeNull] public IReadOnlyList<AtomBase> Children => _children;
        [CanBeNull] public IReadOnlyList<AtomBase> Subscribers => _subscribers;

        public int SubscribersCount => _subscribers?.Count ?? 0;
        public string DebugName => _debugName;

        public bool IsActive => options.Has(AtomOptions.Active);

        internal enum AtomState
        {
            Obsolete,
            Checking,
            Pulling,
            Actual,
        }

        [Flags]
        public enum AtomOptions
        {
            None = 0,
            AutoActualize = 1 << 0,
            Active = 1 << 1,
            
            HasCache = 1 << 2,
            NextDirectEvaluate = 1 << 3,
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

            if (IsActive)
            {
                options.Reset(AtomOptions.Active);
                AtomRegistry.OnInactivate(this);
            }

            State = AtomState.Obsolete;
        }

        public void Actualize(bool force = false)
        {
            if (State == AtomState.Pulling)
            {
                throw new CyclicAtomDependencyException(this);
            }

            if (!force && State == AtomState.Actual)
            {
                return;
            }

            Stack.Push(this);

            if (!IsActive)
            {
                options.Set(AtomOptions.Active);
                AtomRegistry.OnActivate(this);
            }

            if (!force && State == AtomState.Checking)
            {
                for (var i = 0; i < _children.Count; i++)
                {
                    if (State != AtomState.Checking)
                    {
                        break;
                    }

                    _children[i].Actualize();
                }

                if (State == AtomState.Checking)
                {
                    State = AtomState.Actual;
                }
            }

            if (force || State != AtomState.Actual)
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

                State = AtomState.Pulling;

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
            if (State == AtomState.Actual || State == AtomState.Pulling)
            {
                State = AtomState.Checking;
                CheckSubscribers();
            }
        }

        private void Obsolete()
        {
            if (State == AtomState.Obsolete)
            {
                return;
            }

            State = AtomState.Obsolete;
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