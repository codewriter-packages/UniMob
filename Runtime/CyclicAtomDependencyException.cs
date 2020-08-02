using System;

namespace UniMob
{
    public class CyclicAtomDependencyException : Exception
    {
        public CyclicAtomDependencyException(AtomBase source)
            : base("Cyclic atom dependency of " + source)
        {
        }
    }
}