using NUnit.Framework;
using Theraot.Collections.ThreadSafe;

namespace Tests
{
    [TestFixture]
    internal class MapperTests
    {
        [Test]
        public void SetAndGet()
        {
            var mapper = new Mapper<int>();
            const int Input = 42;
            int result;
            Assert.IsTrue(mapper.TrySet(0, Input));
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(Input, result);
        }

        [Test]
        public void SparseData()
        {
            var mapper = new Mapper<int>();
            var data = new[]
            {
                new[] { 24074, 11156 },
                new[] { 22731, 17638 },
                new[] { 17336, 6935 },
                new[] { 8212, 22342 },
                new[] { 29837, 9789 },
                new[] { 14312, 16209 },
                new[] { 29097, 23526 },
                new[] { 20631, 31667 },
                new[] { 2752, 14885 },
                new[] { 25359, 30225 },
                new[] { 4583, 17329 },
                new[] { 26648, 28111 },
                new[] { 6948, 32130 },
                new[] { 00498,   4487 },
                new[] { 31105, 6313 },
                new[] { 26398, 16772 },
                new[] { 3644, 32520 },
                new[] { 25228, 5511 },
                new[] { 10169, 23587 },
                new[] { 8148, 15974 },
                new[] { 20480, 4628 },
                new[] { 8739, 18591 },
                new[] { 28713, 22060 },
                new[] { 18476, 21862 },
                new[] { 6821, 24167 },
                new[] { 7038, 10563 },
                new[] { 7570, 20101 },
                new[] { 7718, 32320 },
                new[] { 28587, 25902 },
                new[] { 13350, 31552 },
                new[] { 27450, 15232 },
                new[] { 30662, 24366 }
            };
            foreach (var pair in data)
            {
                Assert.IsTrue(mapper.TrySet(pair[0], pair[1]));
            }
            foreach (var pair in data)
            {
                int result;
                Assert.IsTrue(mapper.TryGet(pair[0], out result));
                Assert.AreEqual(pair[1], result);
            }
        }
    }
}