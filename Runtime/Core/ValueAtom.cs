using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace UniMob.Core
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ValueAtom<T> : AtomBase, MutableAtom<T>
    {
        internal readonly IEqualityComparer<T> comparer;

        internal T value;

        internal ValueAtom(Lifetime lifetime, string debugName, T value)
            : base(lifetime, debugName, AtomOptions.None)
        {
            this.value = value;
            comparer = EqualityComparer<T>.Default;
        }

        public T Value
        {
            get
            {
                Actualize();
                SubscribeToParent();

                return value;
            }
            set
            {
                if (comparer.Equals(value, this.value))
                {
                    return;
                }

                Invalidate();

                this.value = value;
            }
        }

        protected override void Evaluate()
        {
            state = AtomState.Actual;
        }
    }
}