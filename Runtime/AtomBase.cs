using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;

namespace UniMob
{
    public abstract class AtomBase : IEquatable<AtomBase>
    {
        private readonly string _debugName;
        private readonly bool _keepAlive;
        private readonly IAtomCallbacks _callbacks;
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

        internal bool Reaping
        {
            get => _reaping;
            set => _reaping = value;
        }

        public enum AtomState
        {
            Obsolete,
            Checking,
            Pulling,
            Actual,
        }

        protected AtomBase(string debugName, bool keepAlive, IAtomCallbacks callbacks)
        {
            _debugName = debugName;
            _keepAlive = keepAlive;
            _callbacks = callbacks;
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
                _callbacks?.OnInactive();
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
                _callbacks?.OnActive();
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
                    AtomScheduler.Actualize(this);
                }
                else
                {
                    AtomScheduler.Reap(this);
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
                AtomScheduler.Unreap(this);
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
                    AtomScheduler.Reap(this);
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

        public override string ToString()
        {
            return _debugName ?? "[Anonymous]";
        }

        internal static AtomBase Stack;

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