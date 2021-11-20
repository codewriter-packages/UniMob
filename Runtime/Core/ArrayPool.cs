using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Assertions;

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
        public static void Rent(ref T[] array, byte cap)
        {
            Assert.IsNull(array);

            array = AllocateInternal(cap);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Grow(ref T[] array, ref byte cap)
        {
            Assert.IsNotNull(array);
            Assert.AreEqual(1 << cap, array.Length);

            var oldArray = array;
            var oldCap = cap;

            cap += 1;
            array = AllocateInternal(cap);

            for (var i = 0; i < oldArray.Length; i++)
            {
                array[i] = oldArray[i];
                oldArray[i] = null;
            }

            FreeInternal(oldArray, oldCap);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(ref T[] array, byte cap)
        {
            Assert.IsNotNull(array);
            Assert.AreEqual(1 << cap, array.Length);

#if UNITY_ASSERTIONS
            foreach (var val in array)
            {
                Assert.IsNull(val);
            }
#endif

            FreeInternal(array, cap);

            array = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T[] AllocateInternal(byte cap)
        {
            var stack = Pool[cap];
            return stack.Count > 0 ? stack.Pop() : new T[1 << cap];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FreeInternal(T[] array, byte cap)
        {
            Pool[cap].Push(array);
        }
    }
}