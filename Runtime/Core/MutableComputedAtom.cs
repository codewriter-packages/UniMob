using System;
using JetBrains.Annotations;

namespace UniMob.Core
{
    public class MutableComputedAtom<T> : ComputedAtom<T>, MutableAtom<T>
    {
        private readonly Action<T> _push;

        internal MutableComputedAtom(
            Lifetime lifetime,
            string debugName,
            [NotNull] Func<T> pull,
            [NotNull] Action<T> push,
            bool keepAlive = false)
            : base(lifetime, debugName, pull, keepAlive)
        {
            _push = push ?? throw new ArgumentNullException(nameof(push));
        }

        public new T Value
        {
            get => base.Value;
            set
            {
                if (options.Has(AtomOptions.HasCache) && comparer.Equals(value, cache))
                {
                    return;
                }

                Invalidate();

                _push(value);
            }
        }
    }
}