using System;
using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace UniMob.Core
{
    [Flags]
    public enum AtomOptions : byte
    {
        None = 0,
        AutoActualize = 1 << 0,
        Active = 1 << 1,
        Disposed = 1 << 2,

        HasCache = 1 << 6,
        NextDirectEvaluate = 1 << 7,
    }

    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    internal static class AtomOptionExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this AtomOptions keys, in AtomOptions flag)
        {
            return (keys & flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Set(this ref AtomOptions keys, in AtomOptions flag)
        {
            keys |= flag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySet(this ref AtomOptions keys, in AtomOptions flag)
        {
            return keys != (keys |= flag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Reset(this ref AtomOptions keys, in AtomOptions flag)
        {
            keys &= ~flag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReset(this ref AtomOptions keys, in AtomOptions flag)
        {
            return keys != (keys &= ~flag);
        }
    }
}