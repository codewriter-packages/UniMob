using System.Collections.Generic;
using System.Diagnostics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Profiling;

namespace UniMob.Core
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AtomScheduler : MonoBehaviour
    {
        public static readonly Stopwatch SyncTimer = new Stopwatch();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly CustomSampler ProfilerSampler = CustomSampler.Create("UniMob.Sync");
#endif
        private static Queue<AtomBase> _updatingCurrentFrame = new Queue<AtomBase>();
        private static Queue<AtomBase> _updatingNextFrame = new Queue<AtomBase>();

        private static AtomScheduler _current;
        private static bool _dirty;

        private void Update()
        {
            if (!_dirty)
            {
                return;
            }

            _dirty = false;

            Sync();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            _current = null;
            _dirty = false;
        }

        internal static void Actualize(AtomBase atom)
        {
            _dirty = true;
            _updatingNextFrame.Enqueue(atom);

            if (ReferenceEquals(_current, null) && Application.isPlaying)
            {
                var go = new GameObject(nameof(AtomScheduler));
                _current = go.AddComponent<AtomScheduler>();
                DontDestroyOnLoad(_current);
            }
        }

        public static void Sync()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ProfilerSampler.Begin();
#endif
            SyncTimer.Restart();

            var toSwap = _updatingCurrentFrame;
            _updatingCurrentFrame = _updatingNextFrame;
            _updatingNextFrame = toSwap;

            while (_updatingCurrentFrame.Count > 0)
            {
                var atom = _updatingCurrentFrame.Dequeue();

                if (atom.options.Has(AtomOptions.Active) && atom.state != AtomState.Actual)
                {
                    atom.Actualize();
                }
            }

            SyncTimer.Stop();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ProfilerSampler.End();
#endif
        }
    }
}