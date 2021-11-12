using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using JetBrains.Annotations;

namespace UniMob.Core
{
    public class ComputedAtom<T> : AtomBase, Atom<T>
    {
        private readonly Func<T> _pull;
        protected readonly IEqualityComparer<T> comparer;

        protected T cache;
        protected ExceptionDispatchInfo exception;

        internal ComputedAtom(
            Lifetime lifetime,
            string debugName,
            [NotNull] Func<T> pull,
            bool keepAlive = false)
            : base(lifetime, debugName, keepAlive ? AtomOptions.AutoActualize : AtomOptions.None)
        {
            _pull = pull ?? throw new ArgumentNullException(nameof(pull));
            comparer = EqualityComparer<T>.Default;
        }

        // for CodeGen
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DirectEvaluate()
        {
            return options.TryReset(AtomOptions.NextDirectEvaluate);
        }

        // for CodeGen
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompareAndInvalidate(T value)
        {
            if (options.Has(AtomOptions.HasCache) && comparer.Equals(value, cache))
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
                if (state == AtomState.Pulling)
                {
                    throw new CyclicAtomDependencyException(this);
                }

                SubscribeToParent();
                Actualize();

                if (exception != null)
                {
                    exception.Throw();
                }

                return cache;
            }
        }

        public override void Deactivate()
        {
            base.Deactivate();

            options.Reset(AtomOptions.HasCache);
            cache = default;
            exception = null;
        }

        protected override void Evaluate()
        {
            bool changed;

            try
            {
                state = AtomState.Pulling;
                options.Set(AtomOptions.NextDirectEvaluate);

                var value = _pull();

                if (options.Has(AtomOptions.HasCache) && comparer.Equals(value, cache))
                {
                    return;
                }

                changed = options.Has(AtomOptions.HasCache) || exception != null;

                options.Set(AtomOptions.HasCache);
                cache = value;
                exception = null;
            }
            catch (Exception ex)
            {
                changed = true;

                options.Reset(AtomOptions.HasCache);
                cache = default;
                exception = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                state = AtomState.Actual;
                options.Reset(AtomOptions.NextDirectEvaluate);
            }

            if (changed)
            {
                ObsoleteSubscribers();
            }
        }

        public void Invalidate()
        {
            state = AtomState.Obsolete;

            options.Reset(AtomOptions.HasCache);
            cache = default;
            exception = null;

            ObsoleteSubscribers();
        }
    }
}