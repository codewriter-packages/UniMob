using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using JetBrains.Annotations;

namespace UniMob
{
    public sealed class ComputedAtom<T> : AtomBase, MutableAtom<T>
    {
        private readonly IEqualityComparer<T> _comparer;
        private readonly AtomPull<T> _pull;
        private readonly AtomPush<T> _push;

        private T _cache;
        private ExceptionDispatchInfo _exception;

        internal ComputedAtom(
            Lifetime lifetime,
            string debugName,
            [NotNull] AtomPull<T> pull,
            AtomPush<T> push,
            bool keepAlive = false)
            : base(lifetime, debugName, keepAlive ? AtomOptions.AutoActualize : AtomOptions.None)
        {
            _pull = pull ?? throw new ArgumentNullException(nameof(pull));
            _push = push;
            _comparer = EqualityComparer<T>.Default;
        }

        // for CodeGen
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DirectEvaluate()
        {
            return options.TryReset(AtomOptions.NextDirectEvaluate);
        }

        // for CodeGen
        public bool CompareAndInvalidate(T value)
        {
            if (options.Has(AtomOptions.HasCache) && _comparer.Equals(value, _cache))
            {
                return false;
            }

            Invalidate();
            return true;
        }

        public T Value
        {
            get
            {
                if (State == AtomState.Pulling)
                {
                    throw new CyclicAtomDependencyException(this);
                }

                SubscribeToParent();
                Actualize();

                if (_exception != null)
                {
                    _exception.Throw();
                }

                return _cache;
            }
            set
            {
                if (_push == null)
                {
                    throw new InvalidOperationException("It is not possible to assign a new value to a readonly Atom");
                }

                using (Atom.NoWatch)
                {
                    if (options.Has(AtomOptions.HasCache) && _comparer.Equals(value, _cache))
                    {
                        return;
                    }

                    Invalidate();


                    _push(value);
                }
            }
        }

        public override void Deactivate()
        {
            base.Deactivate();

            options.Reset(AtomOptions.HasCache);
            _cache = default;
            _exception = null;
        }

        protected override void Evaluate()
        {
            bool changed;

            try
            {
                State = AtomState.Pulling;
                options.Set(AtomOptions.NextDirectEvaluate);

                var value = _pull();

                using (Atom.NoWatch)
                {
                    if (options.Has(AtomOptions.HasCache) && _comparer.Equals(value, _cache))
                    {
                        return;
                    }
                }

                changed = options.Has(AtomOptions.HasCache) || _exception != null;

                options.Set(AtomOptions.HasCache);
                _cache = value;
                _exception = null;
            }
            catch (Exception exception)
            {
                changed = true;

                options.Reset(AtomOptions.HasCache);
                _cache = default;
                _exception = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                State = AtomState.Actual;
                options.Reset(AtomOptions.NextDirectEvaluate);
            }

            if (changed)
            {
                ObsoleteSubscribers();
            }
        }

        public void Invalidate()
        {
            State = AtomState.Obsolete;

            options.Reset(AtomOptions.HasCache);
            _cache = default;
            _exception = null;

            ObsoleteSubscribers();
        }
    }
}