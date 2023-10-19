using System;

namespace UniMob
{
    public static class LifetimeScopeExtension
    {
        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Global
        public static void ThrowIfDisposed(ILifetimeScope scope)
        {
            if (scope.Lifetime.IsDisposed)
            {
                throw new ObjectDisposedException("Lifetime is disposed");
            }
        }
    }
}