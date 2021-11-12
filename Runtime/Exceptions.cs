using System;
using UniMob.Core;

namespace UniMob
{
    public class CyclicAtomDependencyException : Exception
    {
        internal CyclicAtomDependencyException(AtomBase source)
            : base("Cyclic atom dependency of " + source)
        {
        }
    }
}