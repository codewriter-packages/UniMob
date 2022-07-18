using System;
using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace UniMob.Core
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
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