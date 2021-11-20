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
        [TestCase(0, ExpectedResult = 1)]
        [TestCase(1, ExpectedResult = 2)]
        [TestCase(3, ExpectedResult = 8)]
        public int Rent(byte cap)
        {
            string[] array = null;
            ArrayPool<string>.Rent(ref array, cap);
            return array.Length;
        }

        [Test]
        public void Return()
        {
            string[] array = null;
            byte cap = 2;
            ArrayPool<string>.Rent(ref array, cap);
            ArrayPool<string>.Return(ref array, cap);
            ArrayPool<string>.Rent(ref array, cap);
            ArrayPool<string>.Return(ref array, cap);

            Assert.IsNull(array);
            Assert.AreEqual(1, ArrayPool<string>.Pool[cap].Count);
        }

        [Test]
        public void Grow()
        {
            string[] array = null;
            byte cap = 2;
            ArrayPool<string>.Rent(ref array, cap);
            array[0] = "1";
            array[3] = "2";

            var oldArray = array;

            ArrayPool<string>.Grow(ref array, ref cap);

            Assert.IsNotNull(array);
            Assert.AreEqual(8, array.Length);
            Assert.AreEqual("1", array[0]);
            Assert.AreEqual(null, array[1]);
            Assert.AreEqual(null, array[2]);
            Assert.AreEqual("2", array[3]);

            string[] newArray = null;
            ArrayPool<string>.Rent(ref newArray, 2);
            Assert.AreEqual(oldArray, newArray);
            Assert.AreEqual(null, newArray[0]);
            Assert.AreEqual(null, newArray[1]);
            Assert.AreEqual(null, newArray[2]);
            Assert.AreEqual(null, newArray[3]);
        }
    }
}