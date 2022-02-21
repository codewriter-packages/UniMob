using NUnit.Framework;
using UniMob.Core;

namespace UniMob.Tests
{
    public class ArrayUtilityTests
    {
        [TearDown]
        public void TearDown()
        {
            ArrayPool<string>.ClearCache();
        }

        [Test]
        [TestCase(1, ExpectedResult = 1)]
        [TestCase(2, ExpectedResult = 2)]
        [TestCase(8, ExpectedResult = 8)]
        public int Rent(int len)
        {
            ArrayPool<string>.Rent(out var array, len);
            return array.Length;
        }

        [Test]
        public void Return()
        {
            ArrayPool<string>.Rent(out var array, 2);
            ArrayPool<string>.Return(ref array);
            ArrayPool<string>.Rent(out array, 2);
            ArrayPool<string>.Return(ref array);

            Assert.IsNull(array);
            Assert.AreEqual(1, ArrayPool<string>.Pool[1].Count);
        }

        [Test]
        public void Grow()
        {
            ArrayPool<string>.Rent(out var array, 4);
            array[0] = "1";
            array[3] = "2";

            var oldArray = array;

            ArrayPool<string>.Grow(ref array);

            Assert.IsNotNull(array);
            Assert.AreEqual(8, array.Length);
            Assert.AreEqual("1", array[0]);
            Assert.AreEqual(null, array[1]);
            Assert.AreEqual(null, array[2]);
            Assert.AreEqual("2", array[3]);

            ArrayPool<string>.Rent(out var newArray, 4);
            Assert.AreEqual(oldArray, newArray);
            Assert.AreEqual(null, newArray[0]);
            Assert.AreEqual(null, newArray[1]);
            Assert.AreEqual(null, newArray[2]);
            Assert.AreEqual(null, newArray[3]);
        }
    }
}