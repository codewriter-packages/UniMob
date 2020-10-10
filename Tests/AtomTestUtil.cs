using System;

namespace UniMob.Tests
{
    public static class AtomTestUtil
    {
        public static int SubscribersCount<T>(this Atom<T> atom)
        {
            if (atom is AtomBase atomBase)
            {
                return atomBase.SubscribersCount;
            }

            throw new InvalidOperationException($"{nameof(atom)} is not AtomBase");
        }
    }
}