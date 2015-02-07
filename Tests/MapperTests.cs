using NUnit.Framework;
using System;
using System.Threading;
using Theraot.Collections.ThreadSafe;

namespace Tests
{
    [TestFixture]
    internal class MapperTests
    {
        [Test]
        public void CheckIsNew()
        {
            var mapper = new Mapper<int>();
            const int Input_A = 21;
            const int Input_B = 42;
            int result;
            bool isNew;

            mapper.Set(0, Input_A, out isNew);
            Assert.IsTrue(isNew);
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(Input_A, result);

            mapper.Set(0, Input_B, out isNew);
            Assert.IsFalse(isNew);
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(Input_B, result);

            Assert.AreEqual(1, mapper.Count);
        }

        [Test]
        public void CopyTo()
        {
            var mapper = new Mapper<int>();
            var data = GetSampleData();
            foreach (var pair in data)
            {
                bool isNew;
                mapper.Set(pair[0], pair[1], out isNew);
                Assert.IsTrue(isNew);
            }
            Assert.AreEqual(data.Length, mapper.Count);
            var target = new int[data.Length];
            Array.Sort(data, (pairA, pairB) => pairA[0].CompareTo(pairB[0]));
            mapper.CopyTo(target, 0);
            for (int index = 0; index < data.Length; index++)
            {
                var pair = data[index];
                var item = target[index];
                Assert.AreEqual(pair[1], item);
            }
        }

        [Test]
        public void Exchange()
        {
            var mapper = new Mapper<int>();
            const int Input_A = 21;
            const int Input_B = 42;
            int result;
            bool isNew;

            mapper.Set(0, Input_A, out isNew);
            Assert.IsTrue(isNew);
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(Input_A, result);

            Assert.IsFalse(mapper.Exchange(0, Input_B, out result));
            Assert.AreEqual(Input_A, result);
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(Input_B, result);

            Assert.IsTrue(mapper.Exchange(1, Input_A, out result));
            Assert.AreEqual(default(int), result);
            Assert.IsTrue(mapper.TryGet(1, out result));
            Assert.AreEqual(Input_A, result);

            Assert.AreEqual(2, mapper.Count);
        }

        [Test]
        public void GetNotExisting()
        {
            var mapper = new Mapper<int>();
            int result;
            Assert.IsFalse(mapper.TryGet(0, out result));
            Assert.AreEqual(default(int), result);
            Assert.AreEqual(0, mapper.Count);
        }

        [Test]
        public void Insert()
        {
            var mapper = new Mapper<int>();
            const int Input_A = 21;
            const int Input_B = 42;
            int result;

            Assert.IsTrue(mapper.Insert(0, Input_A));
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(Input_A, result);

            Assert.IsFalse(mapper.Insert(0, Input_B));
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(Input_A, result);

            Assert.IsFalse(mapper.Insert(0, Input_A, out result));
            Assert.AreEqual(Input_A, result);
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(Input_A, result);

            Assert.AreEqual(1, mapper.Count);
        }

        [Test]
        public void InsertOrPrevious()
        {
            var mapper = new Mapper<int>();
            const int Input_A = 21;
            const int Input_B = 42;
            int result;

            Assert.IsTrue(mapper.Insert(0, Input_A, out result));
            Assert.AreEqual(0, result);

            Assert.IsFalse(mapper.Insert(0, Input_B, out result));
            Assert.AreEqual(Input_A, result);

            Assert.IsFalse(mapper.Insert(0, Input_A, out result));
            Assert.AreEqual(Input_A, result);
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(Input_A, result);

            Assert.AreEqual(1, mapper.Count);
        }

        [Test]
        public void RemoveAt()
        {
            var mapper = new Mapper<int>();
            const int Input = 21;
            int result;
            bool isNew;

            //---

            mapper.Set(0, Input, out isNew);
            Assert.IsTrue(isNew);
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(Input, result);

            Assert.IsTrue(mapper.RemoveAt(0, out result));
            Assert.AreEqual(Input, result);
            Assert.IsFalse(mapper.TryGet(0, out result));

            Assert.IsFalse(mapper.RemoveAt(1, out result));
            Assert.IsFalse(mapper.TryGet(1, out result));

            //---

            mapper.Set(0, Input, out isNew);
            Assert.IsTrue(isNew);
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(Input, result);

            Assert.IsTrue(mapper.RemoveAt(0));
            Assert.IsFalse(mapper.TryGet(0, out result));

            Assert.IsFalse(mapper.RemoveAt(1));
            Assert.IsFalse(mapper.TryGet(1, out result));

            Assert.AreEqual(0, mapper.Count);
        }

        [Test]
        public void SetAndGet()
        {
            var mapper = new Mapper<int>();
            const int Input = 42;
            int result;
            bool isNew;
            mapper.Set(0, Input, out isNew);
            Assert.IsTrue(isNew);
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(Input, result);
            Assert.AreEqual(1, mapper.Count);
        }

        [Test]
        public void SetToNull()
        {
            var mapper = new Mapper<string>();
            string result;
            bool isNew;

            Assert.IsTrue(mapper.Insert(0, null));
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(null, result);

            mapper.Set(0, "Hello", out isNew);
            Assert.IsFalse(isNew);
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual("Hello", result);

            mapper.Set(0, null, out isNew);
            Assert.IsFalse(isNew);
            Assert.IsTrue(mapper.TryGet(0, out result));
            Assert.AreEqual(null, result);

            mapper.Exchange(0, "Hello", out result);
            Assert.AreEqual(null, result);

            mapper.Exchange(0, null, out result);
            Assert.AreEqual("Hello", result);

            Assert.AreEqual(1, mapper.Count);
        }

        [Test]
        public void SparseData()
        {
            var mapper = new Mapper<int>();
            var data = GetSampleData();
            foreach (var pair in data)
            {
                bool isNew;
                mapper.Set(pair[0], pair[1], out isNew);
                Assert.IsTrue(isNew);
            }
            Assert.AreEqual(data.Length, mapper.Count);
            foreach (var pair in data)
            {
                int result;
                Assert.IsTrue(mapper.TryGet(pair[0], out result));
                Assert.AreEqual(pair[1], result);
            }
            Array.Sort(data, (pairA, pairB) => pairA[0].CompareTo(pairB[0]));
            var index = 0;
            foreach (var item in mapper)
            {
                Assert.AreEqual(data[index][1], item);
                index++;
            }
            Assert.AreEqual(mapper.Count, index);
        }

        [Test]
        public void ThreeThreadsRemoveAt()
        {
            var mapper = new Mapper<int>();
            const int Input = 21;
            var info = new CircularBucket<string>(16);

            var startEvent = new ManualResetEvent(false);
            int[] done = { 0 };
            var threads = new[]
            {
                new Thread
                (
                    () =>
                    {
                        int result;
                        bool isNew;
                        mapper.Set(0, Input, out isNew);
                        info.Add("A: Set - was new: " + isNew);
                        bool removed = mapper.RemoveAt(0, out result);
                        info.Add(removed ? "A: Removed" : "A: Could not remove");
                        Interlocked.Increment(ref done[0]);
                    }
                ),
                new Thread
                (
                    () =>
                    {
                        int result;
                        bool isNew;
                        mapper.Set(0, Input, out isNew);
                        info.Add("B: Set - was new: " + isNew);
                        bool removed = mapper.RemoveAt(0, out result);
                        info.Add(removed ? "B: Removed" : "B: Could not remove");
                        Interlocked.Increment(ref done[0]);
                    }
                ),
                new Thread
                (
                    () =>
                    {
                        int result;
                        bool removed = mapper.RemoveAt(0, out result);
                        info.Add(removed ? "C: Removed" : "C: Could not remove");
                        Interlocked.Increment(ref done[0]);
                    }
                )
            };
            threads[0].Start();
            threads[1].Start();
            threads[2].Start();
            startEvent.Set();
            while (true)
            {
                if (Thread.VolatileRead(ref done[0]) == 3)
                {
                    break;
                }
                Thread.Sleep(0);
            }
            Assert.AreEqual(0, mapper.Count);
        }

        [Test]
        public void TwoThreads()
        {
            var mapper = new Mapper<int>();
            var startEvent = new ManualResetEvent(false);
            var threads = new[]
            {
                new Thread
                (
                    () =>
                    {
                        startEvent.WaitOne();
                        mapper.Insert(0, 2);
                    }
                ),
                new Thread
                (
                    () =>
                    {
                        startEvent.WaitOne();
                        mapper.Insert(0, 4);
                    }
                )
            };
            threads[0].Start();
            threads[1].Start();
            startEvent.Set();
            threads[0].Join();
            threads[1].Join();
            Assert.AreEqual(1, mapper.Count);
        }

        [Test]
        public void TwoThreadsRemoveAt()
        {
            var mapper = new Mapper<int>();
            const int Input = 21;
            var info = new CircularBucket<string>(16);

            var startEvent = new ManualResetEvent(false);
            int[] done = {0};
            var threads = new[]
            {
                new Thread
                (
                    () =>
                    {
                        int result;
                        bool isNew;
                        mapper.Set(0, Input, out isNew);
                        info.Add("A: Set - was new: " + isNew);
                        bool removed = mapper.RemoveAt(0, out result);
                        info.Add(removed ? "A: Removed" : "A: Could not remove");
                        Interlocked.Increment(ref done[0]);
                    }
                ),
                new Thread
                (
                    () =>
                    {
                        int result;
                        bool isNew;
                        mapper.Set(0, Input, out isNew);
                        info.Add("B: Set - was new: " + isNew);
                        bool removed = mapper.RemoveAt(0, out result);
                        info.Add(removed ? "B: Removed" : "B: Could not remove");
                        Interlocked.Increment(ref done[0]);
                    }
                )
            };
            threads[0].Start();
            threads[1].Start();
            startEvent.Set();
            while (true)
            {
                if (Thread.VolatileRead(ref done[0]) == 2)
                {
                    break;
                }
                Thread.Sleep(0);
            }
            Assert.AreEqual(0, mapper.Count);
        }

        private static int[][] GetSampleData()
        {
            return new[]
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
                new[] { 498, 4487 },
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
                new[] { 30662, 24366 },
                new[] { 977, 15 },
                new[] { 777, 344 },
                new[] { 303, 576 },
                new[] { 21, 428 },
                new[] { 722, 711 },
                new[] { 350, 657 },
                new[] { 552, 74 },
                new[] { 738, 381 },
                new[] { 787, 640 },
                new[] { 781, 813 },
                new[] { 679, 334 },
                new[] { 192, 190 },
                new[] { 385, 272 },
                new[] { 760, 942 },
                new[] { 380, 802 },
                new[] { 56, 11 },
                new[] { 87, 33 },
                new[] { 34, 96 },
                new[] { 81, 58 },
                new[] { 43, 45 },
                new[] { 4, 50 },
                new[] { 26, 89 },
                new[] { 31, 83 },
                new[] { 60, 1 },
                new[] { 20, 76 },
                new[] { 32, 71 },
                new[] { 66, 47 },
                new[] { 2, 18 },
                new[] { 75, 37 },
                new[] { 0, 0 },
                new[] { 1, 1 },
                new[] { 3, 3 }
            };
        }
    }
}