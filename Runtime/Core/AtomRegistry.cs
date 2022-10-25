using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace UniMob.Core
{
    internal static class AtomRegistry
    {
        public static HashSet<AtomBase> Active { get; } = new HashSet<AtomBase>();

        public static event Action<AtomBase> OnBecameActive = delegate { };
        public static event Action<AtomBase> OnBecameInactive = delegate { };

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void EditorInitialize()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                foreach (var atom in Active.ToList())
                {
                    atom.Deactivate();
                }

                if (AtomBase.TrackedAtomsCount != 0)
                {
                    UnityEngine.Debug.LogError(
                        "Atoms were incorrectly deactivated on exiting play mode. Please open issue on github");
                }
            }
        }
#endif

        [Conditional("UNITY_EDITOR")]
        public static void OnActivate(AtomBase atom)
        {
            Active.Add(atom);
            OnBecameActive(atom);
        }

        [Conditional("UNITY_EDITOR")]
        public static void OnInactivate(AtomBase atom)
        {
            Active.Remove(atom);
            OnBecameInactive(atom);
        }
    }
}