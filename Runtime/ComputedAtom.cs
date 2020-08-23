using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using JetBrains.Annotations;

namespace UniMob
{
    public sealed class ComputedAtom<T> : AtomBase, MutableAtom<T>
    {
        private readonly IEqualityComparer<T> _comparer;
        private readonly AtomPull<T> _pull;
        private readonly AtomPush<T> _push;
        private readonly bool _requiresReaction;

        private bool _hasCache;
        private T _cache;
        private ExceptionDispatchInfo _exception;
        private bool _nextDirectEvaluate;

        // for CodeGen
        public ComputedAtom(
            string debugName,
            AtomPull<T> pull)
            : this(debugName, pull, null)
        {
        }

        internal ComputedAtom(
            string debugName,
            [NotNull] AtomPull<T> pull,
            AtomPush<T> push = null,
            bool keepAlive = false,
            bool requiresReaction = false,
            IAtomCallbacks callbacks = null,
            IEqualityComparer<T> comparer = null)
            : base(debugName, keepAlive, callbacks)
        {
            _pull = pull ?? throw new ArgumentNullException(nameof(pull));
            _push = push;
            _comparer = comparer ?? EqualityComparer<T>.Default;
            _requiresReaction = requiresReaction;
        }

        // for CodeGen
        public bool DirectEvaluate()
        {
            if (_nextDirectEvaluate)
            {
                _nextDirectEvaluate = false;
                return true;
            }

            return false;
        }

        public T Value
        {
            get
            {
                if (State == AtomState.Pulling)
                {
                    throw new CyclicAtomDependencyException(this);
                }

                if (!IsActive && Stack.Peek() == null && !KeepAlive)
                {
                    WarnAboutUnTrackedRead();

                    var oldState = State;
                    try
                    {
                        State = AtomState.Pulling;
                        _nextDirectEvaluate = true;
                        return _pull();
                    }
                    finally
                    {
                        State = oldState;
                        _nextDirectEvaluate = false;
                    }
                }

                Actualize();
                SubscribeToParent();

                if (_exception != null)
                {
                    _exception.Throw();
                }

                return _cache;
            }
            set
            {
                if (_push == null)
                    throw new InvalidOperationException("It is not possible to assign a new value to a readonly Atom");

                using (Atom.NoWatch)
                {
                    if (_hasCache && _comparer.Equals(value, _cache))
                        return;

                    Invalidate();


                    _push(value);
                }
            }
        }

        public override void Deactivate()
        {
            base.Deactivate();

            _hasCache = false;
            _cache = default;
            _exception = null;
        }

        protected override void Evaluate()
        {
            try
            {
                State = AtomState.Pulling;
                _nextDirectEvaluate = true;

                var value = _pull();

                using (Atom.NoWatch)
                {
                    if (_hasCache && _comparer.Equals(value, _cache))
                        return;
                }

                _hasCache = true;
                _cache = value;
                _exception = null;
            }
            catch (Exception exception)
            {
                _hasCache = false;
                _cache = default;
                _exception = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                State = AtomState.Actual;
                _nextDirectEvaluate = false;
            }

            ObsoleteSubscribers();
        }

        public void Invalidate()
        {
            State = AtomState.Obsolete;

            _hasCache = false;
            _cache = default;
            _exception = null;

            ObsoleteSubscribers();
        }

        private void WarnAboutUnTrackedRead()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_requiresReaction)
            {
                throw new InvalidOperationException(
                    $"[UniMob] Computed value is read outside a reactive context");
            }
#endif
        }
    }
}