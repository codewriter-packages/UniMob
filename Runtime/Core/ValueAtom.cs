using System.Collections.Generic;

namespace UniMob.Core
{
    public class ValueAtom<T> : AtomBase, MutableAtom<T>
    {
        internal readonly IEqualityComparer<T> comparer;

        internal T value;

        internal ValueAtom(string debugName, T value)
        {
            this.debugName = debugName;
            this.value = value;
            options = AtomOptions.None;
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