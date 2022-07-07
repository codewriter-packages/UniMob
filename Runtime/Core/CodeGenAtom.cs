using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UniMob.Core
{
    public static class CodeGenAtom
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComputedAtom<T> Create<T>(ILifetimeScope scope, string debugName, Func<T> pull,
            bool keepAlive)
        {
            var atom = new ComputedAtom<T>(debugName, pull, keepAlive);
            scope.Lifetime.Register(atom);
            return atom;
        }

        public static ComputedAtom<T> CreatePooled<T>(ILifetimeScope scope, string debugName, Func<T> pull,
            bool keepAlive)
        {
            var atomPool = Pool<T>.Atoms;

            ComputedAtom<T> atom;
            if (atomPool.Count > 0)
            {
                atom = atomPool.Pop();
                atom.Setup(debugName, pull, keepAlive);
            }
            else
            {
                atom = new ComputedAtom<T>(debugName, pull, keepAlive);
            }

            var disposerPool = Pool<T>.Recyclers;
            var disposer = disposerPool.Count > 0 ? disposerPool.Pop() : new Recycler<T>();
            disposer.Setup(atom);

            scope.Lifetime.Register(disposer);

            return atom;
        }

        internal static class Pool<T>
        {
            public static readonly Stack<ComputedAtom<T>> Atoms = new Stack<ComputedAtom<T>>();
            public static readonly Stack<Recycler<T>> Recyclers = new Stack<Recycler<T>>();

            public static void Clear()
            {
                Atoms.Clear();
                Recyclers.Clear();
            }
        }

        internal class Recycler<T> : IDisposable
        {
            private ComputedAtom<T> _atom;

            public void Setup(ComputedAtom<T> atom)
            {
                _atom = atom;
            }

            public void Dispose()
            {
                ((IDisposable) _atom).Dispose();

                Pool<T>.Atoms.Push(_atom);
                Pool<T>.Recyclers.Push(this);

                _atom = null;
            }
        }
    }
}