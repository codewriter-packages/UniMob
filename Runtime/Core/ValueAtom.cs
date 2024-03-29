using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace UniMob.Core
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ValueAtom<T> : AtomBase, MutableAtom<T>
    {
        internal T value;

        internal ValueAtom(string debugName, T value)
        {
            this.debugName = debugName;
            this.value = value;
            options = AtomOptions.None;
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
                if (EqualityComparer<T>.Default.Equals(value, this.value))
                {
                    return;
                }

                Invalidate();

                this.value = value;
            }
        }

        protected override void Evaluate(bool activating)
        {
            state = AtomState.Actual;
        }
    }
}