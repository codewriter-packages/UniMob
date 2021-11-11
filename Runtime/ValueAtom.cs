using System.Collections.Generic;

namespace UniMob
{
    internal class ValueAtom<T> : AtomBase, MutableAtom<T>
    {
        private readonly IEqualityComparer<T> _comparer;
        private T _value;

        internal ValueAtom(Lifetime lifetime, string debugName, T value)
            : base(lifetime, debugName, AtomOptions.None)
        {
            _value = value;
            _comparer = EqualityComparer<T>.Default;
        }

        public T Value
        {
            get
            {
                SubscribeToParent();
                Actualize();

                return _value;
            }
            set
            {
                using (Atom.NoWatch)
                {
                    if (_comparer.Equals(value, _value))
                    {
                        return;
                    }

                    State = AtomState.Actual;
                    _value = value;

                    ObsoleteSubscribers();
                }
            }
        }

        protected override void Evaluate()
        {
            State = AtomState.Actual;
        }

        public void Invalidate()
        {
            ObsoleteSubscribers();
        }
    }
}