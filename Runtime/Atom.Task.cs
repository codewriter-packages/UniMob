using System;
using System.Threading.Tasks;
using UniMob.Core;

namespace UniMob
{
    public static partial class Atom
    {
        /// <summary>
        /// Creates an atom from Task.
        /// </summary>
        /// <param name="lifetime">Atom lifetime.</param>
        /// <param name="func"></param>
        /// <param name="debugName">Debug name for this atom.</param>
        /// <typeparam name="T">Atom value type.</typeparam>
        /// <returns>Created atom.</returns>
        public static AsyncAtom<T> FromTask<T>(
            Lifetime lifetime,
            Func<Lifetime, Task<T>> func,
            string debugName = null)
        {
            NestedLifetimeDisposer? taskDisposer = null;
            Lifetime taskLifetime = default;
            AsyncSinkAtom<T> atom = null;

            atom = new AsyncSinkAtom<T>(debugName, AtomAsyncValue.Loading<T>(), Load, Cancel, Reload);
            lifetime.Register(atom);
            return atom;

            void Load(AtomSink<AtomAsyncValue<T>> sink)
            {
                LoadAsync();
            }

            async void LoadAsync(bool clearValue = true)
            {
                taskDisposer?.Dispose();
                taskDisposer = lifetime.CreateNested(out taskLifetime);

                // ReSharper disable PossibleNullReferenceException
                // ReSharper disable AccessToModifiedClosure

                try
                {
                    if (!clearValue && atom.options.Has(AtomOptions.HasCache) && atom.value.HasValue)
                    {
                        atom.SetValue(AtomAsyncValue.Loading(atom.value.value));
                    }
                    else
                    {
                        atom.SetValue(AtomAsyncValue.Loading<T>());
                    }

                    var task = func.Invoke(taskLifetime);
                    var result = await task;

                    atom.SetValue(AtomAsyncValue.Value(result));
                }
                catch (Exception ex)
                {
                    atom.SetException(ex);
                }
                finally
                {
                    taskDisposer?.Dispose();
                    taskDisposer = null;
                }

                // ReSharper restore AccessToModifiedClosure
                // ReSharper restore PossibleNullReferenceException
            }

            void Cancel()
            {
                taskDisposer?.Dispose();
                taskDisposer = null;
            }

            void Reload(bool clearValue)
            {
                Cancel();
                LoadAsync(clearValue);
            }
        }
    }
}