using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Unity.IL2CPP.CompilerServices;

namespace UniMob.Core
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class ComputedAtom<T> : AtomBase, MutableAtom<T>
    {
        internal static Stack<ComputedAtom<T>> Pool;

        internal Func<T> pull;
        internal Action<T> push;

        internal T cache;
        internal ExceptionDispatchInfo exception;

        internal ComputedAtom(
            string debugName,
            Func<T> pull,
            Action<T> push,
            AtomOptions options)
        {
            this.debugName = debugName;
            this.pull = pull;
            this.push = push;
            this.options = options;
        }

        internal void Setup(string debugName, Func<T> pull, Action<T> push, AtomOptions options)
        {
            if (!this.options.Has(AtomOptions.Disposed))
            {
                throw new InvalidOperationException("Cannot reuse non disposed atom");
            }

            this.debugName = debugName;
            this.pull = pull;
            this.push = push;
            this.options = options;
        }

        public override void Dispose()
        {
            base.Dispose();

            if (options.Has(AtomOptions.AutoReturnToPool))
            {
                GetPool().Push(this);
            }
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
            if (options.Has(AtomOptions.HasCache) && EqualityComparer<T>.Default.Equals(value, cache))
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
            set
            {
                if (CompareAndInvalidate(value))
                {
                    push(value);
                }
            }
        }

        public override void Deactivate()
        {
            base.Deactivate();

            options.Reset(AtomOptions.HasCache);
            cache = default;
            exception = null;
        }

        protected override void Evaluate(bool activating)
        {
            bool changed;

            try
            {
                state = AtomState.Pulling;
                options.Set(AtomOptions.NextDirectEvaluate);

                var value = pull();

                if (options.Has(AtomOptions.HasCache) && EqualityComparer<T>.Default.Equals(value, cache))
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

        internal static Stack<ComputedAtom<T>> GetPool()
        {
            if (Pool != null)
            {
                return Pool;
            }

            return Pool = new Stack<ComputedAtom<T>>();
        }
    }
}