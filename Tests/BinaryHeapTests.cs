using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace UniMob.Tests
{
    public class BinaryHeapTests
    {
        [Test]
        public void Add()
        {
            var srcArray = Enumerable.Range(1, 10000).ToArray();
            var rndArray = srcArray.ToArray();
            
            var rnd = new Random(354521);
            for (int t = 0; t < rndArray.Length; t++ )
            {
                var tmp = rndArray[t];
                var rndIndex = rnd.Next(t, rndArray.Length);
                rndArray[t] = rndArray[rndIndex];
                rndArray[rndIndex] = tmp;
            }
         
            var heap = new BinaryHeap<int, int>(new IntComparer());
            foreach (var item in rndArray)
            {
                heap.Add(item, item);
            }

            foreach (var expected in srcArray)
            {
                var actual = heap.Remove();
                Assert.AreEqual(expected, actual);
            }
        }

        [Test]
        public void Clear()
        {
            var heap = new BinaryHeap<int, string>(new IntComparer());
            heap.Add(1, "one");
            heap.Add(2, "two");
            heap.Add(3, "three");
            heap.Clear();
            Assert.AreEqual(0, heap.Count);
        }

        [Test]
        public void PeekKey()
        {
            var heap = new BinaryHeap<int, string>(new IntComparer());
            heap.Add(1, "one");
            heap.Add(2, "two");
            
            Assert.AreEqual(1, heap.PeekKey());
            Assert.AreEqual(1, heap.PeekKey());
            heap.Remove();
            Assert.AreEqual(2, heap.PeekKey());
        }
        
        [Test]
        public void PeekOnEmpty()
        {
            var heap = new BinaryHeap<int, string>(new IntComparer());
            Assert.Throws<InvalidOperationException>(() => heap.PeekKey());
        }

        [Test]
        public void RemoveOnEmpty()
        {
            var heap = new BinaryHeap<int, string>(new IntComparer());
            Assert.Throws<InvalidOperationException>(() => heap.Remove());
        }

        class IntComparer : IComparer<int>
        {
            public int Compare(int x, int y) => x.CompareTo(y);
        }
    }
}