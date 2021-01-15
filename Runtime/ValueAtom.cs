using System.Collections.Generic;

namespace UniMob
{
    internal class ValueAtom<T> : AtomBase, MutableAtom<T>
    {
        private readonly IEqualityComparer<T> _comparer;
        private T _value;

        internal ValueAtom(
            string debugName,
            T value,
            IAtomCallbacks callbacks,
            IEqualityComparer<T> comparer = null)
            : base(debugName, false, callbacks)
        {
            _value = value;
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public T Value
        {
            get
            {
                if (!IsActive && Stack.Peek() == null && !KeepAlive)
                {
                    return _value;
                }

                Actualize();
                SubscribeToParent();
                return _value;
            }
            set
            {
                using (Atom.NoWatch)
                {
                    if (_comparer.Equals(value, _value))
                        return;

                    State = AtomState.Actual;
                    _value = value;

                    ObsoleteSubscribers();
                }
            }
        }

        protected override void Evaluate()
        {
            State = AtomState.Actual;

            ObsoleteSubscribers();
        }

        public void Invalidate()
        {
            ObsoleteSubscribers();
        }
    }
}