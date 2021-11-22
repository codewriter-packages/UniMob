using System;
using System.Collections.Generic;
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

            private static readonly Stack<AtomBase> WatchStack = new Stack<AtomBase>();

            public static IDisposable Enter()
            {
                WatchStack.Push(AtomBase.Stack);
                AtomBase.Stack = null;

                return Instance;
            }

            public void Dispose()
            {
                AtomBase.Stack = WatchStack.Pop();
            }
        }
    }
}