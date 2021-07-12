using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UniMob
{
    public class AtomDebuggerWindow : EditorWindow
    {
        private static readonly IReadOnlyList<AtomBase> EmptyList = new List<AtomBase>();

        [MenuItem("Window/Analysis/Atom Debugger")]
        private static void Open()
        {
            var window = GetWindow<AtomDebuggerWindow>();
            window.titleContent = new GUIContent("Atom Debugger");
            window.Show();
        }

        [SerializeField] private bool rootsOnly = true;

        private Vector2 _activeAtomsScroll;
        private List<AtomBase> _activeAtoms = new List<AtomBase>();

        private AtomBase _selectedAtom;
        private Vector2 _selectedAtomScroll;

        private void OnEnable()
        {
            Subscribe();
            RefreshActiveAtoms();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            Unsubscribe();
            AtomRegistry.OnBecameActive += OnAtomBecameActive;
            AtomRegistry.OnBecameInactive += OnAtomBecameInactive;
        }

        private void Unsubscribe()
        {
            AtomRegistry.OnBecameActive -= OnAtomBecameActive;
            AtomRegistry.OnBecameInactive -= OnAtomBecameInactive;
        }

        private void OnAtomBecameInactive(AtomBase obj)
        {
            RefreshActiveAtoms();
        }

        private void OnAtomBecameActive(AtomBase obj)
        {
            RefreshActiveAtoms();
        }

        private void RefreshActiveAtoms()
        {
            _activeAtoms = AtomRegistry.Active.ToList();
            _activeAtoms.Sort(AtomNameSorter.Shared);
            Repaint();
        }

        private void OnGUI()
        {
            if (_selectedAtom != null)
            {
                DrawSelectedAtom();
            }
            else
            {
                DrawActiveAtoms();
            }
        }

        private void DrawSelectedAtom()
        {
            // header
            {
                GUILayout.BeginHorizontal(EditorStyles.toolbar);

                if (GUILayout.Button("< Active Atoms", EditorStyles.toolbarButton))
                {
                    _selectedAtom = null;
                    return;
                }

                GUILayout.FlexibleSpace();

                GUILayout.EndHorizontal();
            }

            var state = _selectedAtom.State == AtomBase.AtomState.Actual ? "Actual"
                : _selectedAtom.State == AtomBase.AtomState.Checking ? "Checking"
                : _selectedAtom.State == AtomBase.AtomState.Pulling ? "Pulling"
                : _selectedAtom.State == AtomBase.AtomState.Obsolete ? "Obsolete"
                : "Unknown";

            GUILayout.Label(_selectedAtom.DebugName ?? "[Anonymous]", EditorStyles.largeLabel);
            GUILayout.Label(state, EditorStyles.label);
            if (_selectedAtom.KeepAlive)
            {
                GUILayout.Label("Keep Alive", EditorStyles.boldLabel);
            }

            GUILayout.Space(10);

            _selectedAtomScroll = GUILayout.BeginScrollView(_selectedAtomScroll);
            {
                DrawAtomCollection("Used By", _selectedAtom.Subscribers, _ => true);
                DrawAtomCollection("Uses", _selectedAtom.Children, _ => true);
            }
            GUILayout.EndScrollView();
        }

        private void DrawActiveAtoms()
        {
            // header
            {
                GUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.FlexibleSpace();

                rootsOnly = GUILayout.Toggle(rootsOnly, "Roots Only", EditorStyles.toolbarButton);
                GUILayout.Space(5);

                GUILayout.EndHorizontal();
            }

            _activeAtomsScroll = GUILayout.BeginScrollView(_activeAtomsScroll);
            {
                DrawAtomCollection("Active Atoms", _activeAtoms, atom =>
                {
                    if (rootsOnly)
                    {
                        var isRoot = atom.KeepAlive || (atom is Reaction);
                        return isRoot;
                    }

                    return true;
                });
            }

            GUILayout.EndScrollView();
        }

        private void DrawAtomCollection(string collectionName, IReadOnlyList<AtomBase> list,
            Func<AtomBase, bool> filter)
        {
            list = list ?? EmptyList;

            GUILayout.Label(collectionName, EditorStyles.boldLabel);

            GUILayout.BeginVertical(Styles.BigTitle);

            if (list.Count == 0)
            {
                GUILayout.Label("No atoms");
            }
            else
            {
                foreach (var atom in list)
                {
                    if (!filter(atom))
                    {
                        continue;
                    }

                    DrawAtomButton(atom);
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawAtomButton(AtomBase atom)
        {
            if (GUILayout.Button(atom.DebugName ?? "[Anonymous]", Styles.LeftButton))
            {
                _selectedAtom = atom;
            }
        }

        private class AtomNameSorter : IComparer<AtomBase>
        {
            public static readonly IComparer<AtomBase> Shared = new AtomNameSorter();

            public int Compare(AtomBase x, AtomBase y)
            {
                return string.CompareOrdinal(x?.DebugName ?? string.Empty, y?.DebugName ?? string.Empty);
            }
        }
    }
}