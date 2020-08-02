using System;
using System.Collections.Generic;

namespace UniMob
{
    internal class BinaryHeap<TKey, TValue>
    {
        private struct Entry
        {
            public TKey Key;
            public TValue Value;
        }

        private readonly IComparer<TKey> _comparer;
        private int _count;
        private Entry[] _heap;

        public int Count => _count;

        public BinaryHeap(IComparer<TKey> comparer, int capacity = 16)
        {
            _heap = new Entry[Math.Max(capacity, 2)];
            _count = 0;
            _comparer = comparer;
        }

        public void Clear()
        {
            for (int i = 0; i < _heap.Length; i++)
            {
                _heap[i] = default;
            }

            _count = 0;
        }

        public TKey PeekKey()
        {
            EnsureNotEmpty();
            return _heap[0].Key;
        }

        public void Add(TKey key, TValue value)
        {
            if (_count >= _heap.Length)
            {
                Entry[] newHeap = new Entry[_heap.Length * 2];
                Array.Copy(_heap, newHeap, _heap.Length);
                _heap = newHeap;
            }

            _heap[_count].Key = key;
            _heap[_count].Value = value;

            var index = _count;
            while (index != 0)
            {
                var parent = (index - 1) / 2;

                if (_comparer.Compare(_heap[index].Key, _heap[parent].Key) < 0)
                {
                    var tmp = _heap[parent];
                    _heap[parent] = _heap[index];
                    _heap[index] = tmp;
                    index = parent;
                }
                else
                {
                    break;
                }
            }

            ++_count;
        }

        public TValue Remove()
        {
            EnsureNotEmpty();

            var toRemove = _heap[0];
            _count -= 1;

            if (_count > 0)
            {
                _heap[0] = _heap[_count];
                _heap[_count] = default;

                var swap = 0;
                int index;

                do
                {
                    index = swap;

                    var left = (index * 2) + 1;
                    var right = (index * 2) + 2;

                    if (right < _count)
                    {
                        if (_comparer.Compare(_heap[index].Key, _heap[left].Key) >= 0)
                        {
                            swap = left;
                        }

                        if (_comparer.Compare(_heap[swap].Key, _heap[right].Key) >= 0)
                        {
                            swap = right;
                        }
                    }
                    else if (left < _count)
                    {
                        if (_comparer.Compare(_heap[index].Key, _heap[left].Key) >= 0)
                        {
                            swap = left;
                        }
                    }

                    if (index != swap)
                    {
                        var tmp = _heap[index];
                        _heap[index] = _heap[swap];
                        _heap[swap] = tmp;
                    }
                } while (index != swap);
            }
            else
            {
                _heap[0] = default;
            }

            return toRemove.Value;
        }

        void EnsureNotEmpty()
        {
            if (_count < 1)
            {
                throw new InvalidOperationException("The heap is empty");
            }
        }
    }
}