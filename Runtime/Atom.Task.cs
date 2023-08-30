using System;
using System.Threading;
using System.Threading.Tasks;
using UniMob.Core;
using UnityEngine.Assertions;

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
            Func<CancellationToken, Task<T>> func,
            string debugName = null)
        {
            CancellationTokenSource cts = null;
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
                Assert.IsNull(cts);

                cts = new CancellationTokenSource();

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

                    var task = func.Invoke(cts.Token);
                    var result = await task;

                    atom.SetValue(AtomAsyncValue.Value(result));
                }
                catch (Exception ex)
                {
                    atom.SetException(ex);
                }
                finally
                {
                    cts.Dispose();
                }
            }

            void Cancel()
            {
                Assert.IsNotNull(cts);

                cts.Dispose();
                cts = null;
            }

            void Reload(bool clearValue)
            {
                Cancel();
                LoadAsync(clearValue);
            }
        }
    }
}