using NUnit.Framework;
using System;
using TSLib.Full;

namespace TS3ABotUnitTests
{
	[TestFixture]
	internal class RingQueueTest
	{

		[Test]
		public void RingQueueTest1()
		{
			var q = new RingQueue<int>(3, 5);

			q.Set(0, 42);

			Assert.True(q.TryPeekStart(0, out int ov));
			Assert.AreEqual(ov, 42);

			q.Set(1, 43);

			// already set
			Assert.Throws<ArgumentOutOfRangeException>(() => q.Set(1, 99));

			Assert.True(q.TryPeekStart(0, out ov));
			Assert.AreEqual(ov, 42);
			Assert.True(q.TryPeekStart(1, out ov));
			Assert.AreEqual(ov, 43);

			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 42);

			Assert.True(q.TryPeekStart(0, out ov));
			Assert.AreEqual(ov, 43);
			Assert.False(q.TryPeekStart(1, out ov));

			q.Set(3, 45);
			q.Set(2, 44);

			// buffer overfull
			Assert.Throws<ArgumentOutOfRangeException>(() => q.Set(4, 99));

			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 43);
			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 44);

			q.Set(4, 46);

			// out of mod range
			Assert.Throws<ArgumentOutOfRangeException>(() => q.Set(5, 99));

			q.Set(0, 47);

			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 45);
			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 46);
			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 47);

			q.Set(2, 49);

			Assert.False(q.TryDequeue(out ov));

			q.Set(1, 48);

			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 48);
			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 49);
		}

		[Test]
		public void RingQueueTest2()
		{
			var q = new RingQueue<int>(50, ushort.MaxValue + 1);

			for (int i = 0; i < ushort.MaxValue - 10; i++)
			{
				q.Set(i, i);
				Assert.True(q.TryDequeue(out var iCheck));
				Assert.AreEqual(iCheck, i);
			}

			var setStatus = q.IsSet(ushort.MaxValue - 20);
			Assert.True(setStatus.HasFlag(ItemSetStatus.Set));

			for (int i = ushort.MaxValue - 10; i < ushort.MaxValue + 10; i++)
			{
				q.Set(i % (ushort.MaxValue + 1), 42);
			}
		}

		[Test]
		public void RingQueueTest3()
		{
			var q = new RingQueue<int>(100, ushort.MaxValue + 1);

			int iSet = 0;
			for (int blockSize = 1; blockSize < 100; blockSize++)
			{
				for (int i = 0; i < blockSize; i++)
				{
					q.Set(iSet++, i);
				}
				for (int i = 0; i < blockSize; i++)
				{
					Assert.True(q.TryDequeue(out var iCheck));
					Assert.AreEqual(i, iCheck);
				}
			}

			for (int blockSize = 1; blockSize < 100; blockSize++)
			{
				q = new RingQueue<int>(100, ushort.MaxValue + 1);
				for (int i = 0; i < blockSize; i++)
				{
					q.Set(i, i);
				}
				for (int i = 0; i < blockSize; i++)
				{
					Assert.True(q.TryDequeue(out var iCheck));
					Assert.AreEqual(i, iCheck);
				}
			}
		}
	}
}
