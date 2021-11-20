using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace UniMob.Core
{
    public abstract class AtomBase : IEquatable<AtomBase>, IDisposable
    {
        private readonly string _debugName;

        internal byte childrenCap;
        internal int childrenCount;
        internal AtomBase[] children;

        internal byte subscribersCap;
        internal int subscribersCount;
        internal AtomBase[] subscribers;

        internal AtomOptions options;
        internal AtomState state = AtomState.Obsolete;

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
            if (children != null)
            {
                for (var i = 0; i < childrenCount; i++)
                {
                    children[i].RemoveSubscriber(this);
                    children[i] = null;
                }

                childrenCount = 0;
                ArrayPool<AtomBase>.Return(ref children, childrenCap);
                childrenCap = 0;
            }

            if (subscribers != null)
            {
                for (var i = 0; i < subscribersCount; i++)
                {
                    subscribers[i].Check();
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
                for (var i = 0; i < childrenCount; i++)
                {
                    if (state != AtomState.Checking)
                    {
                        break;
                    }

                    children[i].Actualize();
                }

                if (state == AtomState.Checking)
                {
                    state = AtomState.Actual;
                }
            }

            if (force || state != AtomState.Actual)
            {
                for (var i = 0; i < childrenCount; i++)
                {
                    children[i].RemoveSubscriber(this);
                    children[i] = null;
                }

                childrenCount = 0;
                state = AtomState.Pulling;

                Evaluate();
            }

            Stack.Pop();
        }

        protected abstract void Evaluate();

        protected void ObsoleteSubscribers()
        {
            if (subscribers == null)
            {
                return;
            }

            for (var i = 0; i < subscribersCount; i++)
            {
                subscribers[i].Obsolete();
            }
        }

        private void CheckSubscribers()
        {
            if (subscribers != null)
            {
                for (var i = 0; i < subscribersCount; i++)
                {
                    subscribers[i].Check();
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
            if (subscribers == null)
            {
                ArrayPool<AtomBase>.Rent(ref subscribers, subscribersCap);
            }
            else if (subscribers.Length == subscribersCount)
            {
                ArrayPool<AtomBase>.Grow(ref subscribers, ref subscribersCap);
            }

            subscribers[subscribersCount] = subscriber;
            subscribersCount++;
        }

        private void RemoveSubscriber(AtomBase subscriber)
        {
            if (subscribers == null)
            {
                return;
            }

            var index = 0;
            while (subscribers[index] != subscriber)
            {
                index++;
            }

            subscribers[index] = subscribers[subscribersCount - 1];
            subscribers[subscribersCount - 1] = null;
            subscribersCount--;

            if (subscribersCount == 0)
            {
                ArrayPool<AtomBase>.Return(ref subscribers, subscribersCap);
                subscribersCap = 0;
            }
        }

        internal void AddChildren(AtomBase child)
        {
            if (children == null)
            {
                ArrayPool<AtomBase>.Rent(ref children, childrenCap);
            }
            else if (children.Length == childrenCount)
            {
                ArrayPool<AtomBase>.Grow(ref children, ref childrenCap);
            }

            children[childrenCount] = child;
            childrenCount++;
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