using System;
using JetBrains.Annotations;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace UniMob.Core
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class AtomBase : IEquatable<AtomBase>, IDisposable
    {
        public static int TrackedAtomsCount = 0;

        [CanBeNull] internal static AtomBase Stack;

        internal string debugName;

        internal int childrenCount;
        internal AtomBase[] children;

        internal int subscribersCount;
        internal AtomBase[] subscribers;

        internal AtomOptions options;
        internal AtomState state = AtomState.Obsolete;

        public bool Equals(AtomBase other)
        {
            return ReferenceEquals(this, other);
        }

        void IDisposable.Dispose()
        {
            Deactivate();

            options.Set(AtomOptions.Disposed);
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
                ArrayPool<AtomBase>.Return(ref children);
            }

            if (subscribers != null)
            {
                for (var i = 0; i < subscribersCount; i++)
                {
                    subscribers[i].Obsolete();
                }
            }

            if (options.TryReset(AtomOptions.Active))
            {
                AtomRegistry.OnInactivate(this);
            }

            state = AtomState.Obsolete;
        }

        public virtual void Invalidate()
        {
            if (Atom.CurrentScope != null)
            {
                Debug.LogError($"Invalidation of atom ({this}) in watched scope ({Atom.CurrentScope}) is dangerous");
            }

            state = AtomState.Obsolete;

            ObsoleteSubscribers();
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

            if (options.Has(AtomOptions.Disposed))
            {
                Debug.LogError($"Actualization of disposed atom ({this}) can lead to memory leak");
            }

            var parent = Stack;
            Stack = this;

            var activating = options.TrySet(AtomOptions.Active);
            if (activating)
            {
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

                Evaluate(activating);
            }

            Stack = parent;
        }

        protected abstract void Evaluate(bool activating);

        protected void ObsoleteSubscribers()
        {
            if (subscribers != null)
            {
                for (var i = 0; i < subscribersCount; i++)
                {
                    subscribers[i].Obsolete();
                }
            }
            else if (options.Has(AtomOptions.AutoActualize))
            {
                AtomScheduler.Actualize(this);
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
                ++TrackedAtomsCount;
                ArrayPool<AtomBase>.Rent(out subscribers, 2);
            }
            else if (subscribers.Length == subscribersCount)
            {
                ArrayPool<AtomBase>.Grow(ref subscribers);
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
                --TrackedAtomsCount;
                ArrayPool<AtomBase>.Return(ref subscribers);
            }
        }

        internal void AddChildren(AtomBase child)
        {
            if (children == null)
            {
                ArrayPool<AtomBase>.Rent(out children, 2);
            }
            else if (children.Length == childrenCount)
            {
                ArrayPool<AtomBase>.Grow(ref children);
            }

            children[childrenCount] = child;
            childrenCount++;
        }

        protected void SubscribeToParent()
        {
            var parent = Stack;
            if (parent != null)
            {
                AddSubscriber(parent);
                parent.AddChildren(this);
            }
        }

        public override string ToString()
        {
            return debugName ?? "[Anonymous]";
        }
    }
}