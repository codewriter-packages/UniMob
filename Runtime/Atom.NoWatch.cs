using System;
using UniMob.Core;

namespace UniMob
{
    public static partial class Atom
    {
        /// <summary>
        /// Creates a scope which blocks changes propagation. <br/>
        /// <br/>
        /// Must only be used in using statement.
        /// </summary>
        /// <example>
        /// 
        /// using (Atom.NoWatch)
        /// {
        ///     // ...
        /// }
        /// 
        /// </example>
        public static IDisposable NoWatch => WatchScope.Enter();

        private class WatchScope : IDisposable
        {
            private static readonly WatchScope Instance = new WatchScope();

            public static IDisposable Enter()
            {
                AtomBase.Stack.Push(null);

                return Instance;
            }

            public void Dispose()
            {
                AtomBase.Stack.Pop();
            }
        }
    }
}