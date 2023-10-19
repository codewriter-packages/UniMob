using System;
using Unity.IL2CPP.CompilerServices;

namespace UniMob.Core
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class CodeGenAtom
    {
        public static ComputedAtom<T> CreatePooled<T>(ILifetimeScope scope, string debugName, Func<T> pull,
            bool keepAlive)
        {
            var pool = ComputedAtom<T>.GetPool();

            ComputedAtom<T> atom;
            if (pool.Count > 0)
            {
                atom = pool.Pop();
                atom.Setup(debugName, pull, keepAlive);
            }
            else
            {
                var options = AtomOptions.AutoReturnToPool;

                if (keepAlive)
                {
                    options |= AtomOptions.AutoActualize;
                }

                atom = new ComputedAtom<T>(debugName, pull, options);
            }

            scope.Lifetime.Register(atom);

            return atom;
        }
    }
}