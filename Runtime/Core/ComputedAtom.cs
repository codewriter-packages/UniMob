using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using JetBrains.Annotations;
using Unity.IL2CPP.CompilerServices;

namespace UniMob.Core
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ComputedAtom<T> : AtomBase, Atom<T>
    {
        internal Func<T> pull;
        internal IEqualityComparer<T> comparer;

        internal T cache;
        internal ExceptionDispatchInfo exception;

        internal ComputedAtom(
            string debugName,
            [NotNull] Func<T> pull,
            bool keepAlive = false)
        {
            this.debugName = debugName;
            this.pull = pull ?? throw new ArgumentNullException(nameof(pull));
            options = keepAlive ? AtomOptions.AutoActualize : AtomOptions.None;
            comparer = EqualityComparer<T>.Default;
        }

        internal void Setup(string debugName, Func<T> pull, bool keepAlive = false)
        {
            if (!options.Has(AtomOptions.Disposed))
            {
                throw new InvalidOperationException("Cannot reuse non disposed atom");
            }
            
            this.debugName = debugName;
            this.pull = pull ?? throw new ArgumentNullException(nameof(pull));
            options = keepAlive ? AtomOptions.AutoActualize : AtomOptions.None;
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
                Actualize();
                SubscribeToParent();

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

                var value = pull();

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
                changed = options.Has(AtomOptions.HasCache) || exception != null;

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

        public override void Invalidate()
        {
            options.Reset(AtomOptions.HasCache);
            cache = default;
            exception = null;

            base.Invalidate();
        }
    }
}