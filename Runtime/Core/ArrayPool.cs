using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UniMob.Core
{
    internal static class ArrayPool<T> where T : class
    {
        internal static readonly Stack<T[]>[] Pool = new Stack<T[]>[30];

        static ArrayPool()
        {
            for (var i = 0; i < Pool.Length; i++)
            {
                Pool[i] = new Stack<T[]>();
            }
        }

        public static void ClearCache()
        {
            foreach (var stack in Pool)
            {
                stack.Clear();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Rent(out T[] array, int len)
        {
            array = AllocateInternal(len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Grow(ref T[] array)
        {
            var oldArray = array;

            array = AllocateInternal(oldArray.Length * 2);

            for (var i = 0; i < oldArray.Length; i++)
            {
                array[i] = oldArray[i];
                oldArray[i] = null;
            }

            FreeInternal(oldArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(ref T[] array)
        {
            FreeInternal(array);

            array = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T[] AllocateInternal(int len)
        {
            var stack = GetPool(len);
            return stack.Count > 0 ? stack.Pop() : new T[len];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FreeInternal(T[] array)
        {
            GetPool(array.Length).Push(array);
        }

        private static Stack<T[]> GetPool(int len)
        {
            var i = 0;
            while (i < Pool.Length && len != 1 << i)
            {
                i++;
            }

            return Pool[i];
        }
    }
}