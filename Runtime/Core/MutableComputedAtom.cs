using System;
using JetBrains.Annotations;
using Unity.IL2CPP.CompilerServices;

namespace UniMob.Core
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MutableComputedAtom<T> : ComputedAtom<T>, MutableAtom<T>
    {
        internal readonly Action<T> push;

        internal MutableComputedAtom(
            string debugName,
            [NotNull] Func<T> pull,
            [NotNull] Action<T> push,
            bool keepAlive = false)
            : base(debugName, pull, keepAlive)
        {
            this.push = push ?? throw new ArgumentNullException(nameof(push));
        }

        public new T Value
        {
            get => base.Value;
            set
            {
                if (CompareAndInvalidate(value))
                {
                    push(value);
                }
            }
        }
    }
}