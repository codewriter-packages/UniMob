using System;

namespace UniMob
{
    public static class AtomAsyncValue
    {
        public static AtomAsyncValue<T> Loading<T>()
        {
            return new AtomAsyncValue<T>(AtomAsyncOptions.IsLoading, default);
        }

        public static AtomAsyncValue<T> Loading<T>(T value)
        {
            return new AtomAsyncValue<T>(AtomAsyncOptions.HasValue | AtomAsyncOptions.IsLoading, value);
        }

        public static AtomAsyncValue<T> Value<T>(T value)
        {
            return new AtomAsyncValue<T>(AtomAsyncOptions.HasValue, value);
        }
    }

    [Serializable]
    public readonly struct AtomAsyncValue<T>
    {
        internal readonly AtomAsyncOptions options;
        internal readonly T value;

        internal AtomAsyncValue(AtomAsyncOptions options, T value)
        {
            this.options = options;
            this.value = value;
        }

        public bool IsLoading => (options & AtomAsyncOptions.IsLoading) != 0;
        public bool HasValue => (options & AtomAsyncOptions.HasValue) != 0;

        public bool TryGetValue(out T v)
        {
            v = value;
            return HasValue;
        }

        public AtomAsyncValue<TResult> Map<TResult>(Func<T, TResult> f)
        {
            return new AtomAsyncValue<TResult>(options, HasValue ? f.Invoke(value) : default);
        }

        public T OrDefault(T defaultValue)
        {
            return HasValue ? value : defaultValue;
        }

        public T OrDefault(Func<T> defaultValue)
        {
            return HasValue ? value : defaultValue.Invoke();
        }
    }

    [Flags]
    internal enum AtomAsyncOptions
    {
        None = 0,
        HasValue = 1 << 0,
        IsLoading = 1 << 1,
    }
}