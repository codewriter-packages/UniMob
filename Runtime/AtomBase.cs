using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace UniMob
{
    public abstract class AtomBase : IEquatable<AtomBase>
    {
        private readonly bool _keepAlive;
        private readonly Action _onActive;
        private readonly Action _onInactive;
        private List<AtomBase> _children;
        private List<AtomBase> _subscribers;
        private bool _active;
        private bool _reaping;

        public AtomState State = AtomState.Obsolete;

        [CanBeNull] public IReadOnlyList<AtomBase> Children => _children;
        [CanBeNull] public IReadOnlyList<AtomBase> Subscribers => _subscribers;

        public bool KeepAlive => _keepAlive;
        public bool IsActive => _active;
        public int SubscribersCount => _subscribers?.Count ?? 0;

        public enum AtomState
        {
            Obsolete,
            Checking,
            Pulling,
            Actual,
        }

        protected AtomBase(bool keepAlive, Action onActive, Action onInactive)
        {
            _keepAlive = keepAlive;
            _onActive = onActive;
            _onInactive = onInactive;
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

            if (_active)
            {
                _active = false;

                try
                {
                    _onInactive?.Invoke();
                }
                catch (Exception e)
                {
                    Zone.Current.HandleUncaughtException(e);
                }
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
                return;

            var parent = Stack;

            Stack = this;

            if (!_active)
            {
                _active = true;

                try
                {
                    _onActive?.Invoke();
                }
                catch (Exception e)
                {
                    Zone.Current.HandleUncaughtException(e);
                }
            }

            if (!force && State == AtomState.Checking)
            {
                for (int i = 0; i < _children.Count; i++)
                {
                    if (State != AtomState.Checking)
                        break;

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

            Stack = parent;
        }

        protected abstract void Evaluate();

        protected void ObsoleteSubscribers()
        {
            if (_subscribers == null)
                return;

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
            else
            {
                if (KeepAlive)
                {
                    Actualize(this);
                }
                else
                {
                    Reap(this);
                }
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
                return;

            State = AtomState.Obsolete;
            CheckSubscribers();
        }

        protected void AddSubscriber(AtomBase subscriber)
        {
            if (_subscribers == null)
            {
                CreateList(out _subscribers);
                Unreap(this);
            }

            _subscribers.Add(subscriber);
        }

        private void RemoveSubscriber(AtomBase subscriber)
        {
            if (_subscribers == null)
                return;

            _subscribers.Remove(subscriber);

            if (_subscribers.Count == 0)
            {
                DeleteList(ref _subscribers);

                if (!KeepAlive)
                {
                    Reap(this);
                }
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
            var parent = Stack;
            if (parent != null)
            {
                AddSubscriber(parent);
                parent.AddChildren(this);
            }
        }

        internal static AtomBase Stack;

        private static readonly Action DoSyncAction = DoSync;

        private static Queue<AtomBase> _updatingCurrentFrame = new Queue<AtomBase>();
        private static Queue<AtomBase> _updatingNextFrame = new Queue<AtomBase>();
        private static readonly Queue<AtomBase> Reaping = new Queue<AtomBase>();
        private static IZone _scheduled;

        internal static void Actualize(AtomBase atom)
        {
            _updatingNextFrame.Enqueue(atom);
            Schedule();
        }

        private static void Reap(AtomBase atom)
        {
            atom._reaping = true;
            Reaping.Enqueue(atom);
            Schedule();
        }

        private static void Unreap(AtomBase atom)
        {
            atom._reaping = false;
        }

        private static void Schedule()
        {
            if (_scheduled == Zone.Current)
                return;

            Zone.Current.Invoke(DoSyncAction);

            _scheduled = Zone.Current;
        }

        private static readonly PerfWatcher SyncPerf = new PerfWatcher("UniMob.Atom.Sync");

        private static void DoSync()
        {
            if (_scheduled == null)
                return;

            _scheduled = null;

            using (SyncPerf.Watch())
            {
                Sync();
            }
        }

        private static void Sync()
        {
            var toSwap = _updatingCurrentFrame;
            _updatingCurrentFrame = _updatingNextFrame;
            _updatingNextFrame = toSwap;
            
            while (_updatingCurrentFrame.Count > 0)
            {
                var atom = _updatingCurrentFrame.Dequeue();

                if (atom.IsActive && !atom._reaping && atom.State != AtomState.Actual)
                {
                    atom.Actualize();
                }
            }

            while (Reaping.Count > 0)
            {
                var atom = Reaping.Dequeue();
                if (atom._reaping && atom._subscribers == null)
                {
                    atom.Deactivate();
                }
            }
        }

        private static readonly Stack<List<AtomBase>> ListPool = new Stack<List<AtomBase>>();

        private static void CreateList(out List<AtomBase> list)
        {
            list = (ListPool.Count > 0) ? ListPool.Pop() : new List<AtomBase>();
        }

        private static void DeleteList(ref List<AtomBase> list)
        {
            list.Clear();
            ListPool.Push(list);
            list = null;
        }
    }
}