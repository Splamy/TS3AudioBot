using System;
using LockCheck;
using NUnit.Framework;

namespace TS3ABotUnitTests
{
	[TestFixture]
	public class UnitTests
	{
		[Test]
		public void DeadLockCheck()
		{
			var warnings = LockChecker.CheckAll("TS3AudioBot", true);
			Assert.IsTrue(warnings.Count == 0, "At least one possible deadlock detected");
		}
	}
}
