using System;
using System.Runtime.CompilerServices;

namespace UniMob.Core
{
    public static class CodeGenAtom
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComputedAtom<T> Create<T>(ILifetimeScope scope, string debugName, Func<T> pull,
            bool keepAlive)
        {
            return new ComputedAtom<T>(scope.Lifetime, debugName, pull, keepAlive);
        }
    }
}