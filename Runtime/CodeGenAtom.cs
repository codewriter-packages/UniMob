using System;

namespace UniMob
{
    public static class CodeGenAtom
    {
        public static ComputedAtom<T> Create<T>(ILifetimeScope scope, string debugName, AtomPull<T> pull, bool keepAlive)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            var lifetime = scope.Lifetime;
            return new ComputedAtom<T>(lifetime, debugName, pull, null, keepAlive);
        }
    }
}