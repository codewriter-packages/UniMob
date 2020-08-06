using System;
using System.Collections.Generic;

namespace UniMob
{
    public class ValueAtom<T> : AtomBase, MutableAtom<T>
    {
        private readonly IEqualityComparer<T> _comparer;
        private T _value;

        internal ValueAtom(
            T value,
            IAtomCallbacks callbacks,
            IEqualityComparer<T> comparer = null)
            : base(false, callbacks)
        {
            _value = value;
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public T Value
        {
            get
            {
                if (!IsActive && Stack == null && !KeepAlive)
                {
                    return _value;
                }

                Actualize();
                SubscribeToParent();
                return _value;
            }
            set
            {
                try
                {
                    using (Atom.NoWatch)
                    {
                        if (_comparer.Equals(value, _value))
                            return;

                        State = AtomState.Actual;
                        _value = value;
                    }
                }
                finally
                {
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

        public override string ToString()
        {
            return Convert.ToString(_value);
        }
    }
}