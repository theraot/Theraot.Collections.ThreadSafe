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
    }
}