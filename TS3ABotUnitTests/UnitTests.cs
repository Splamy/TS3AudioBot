using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LockCheck;

namespace TS3ABotUnitTests
{
	[TestClass]
	public class UnitTests
	{
		[TestMethod]
		public void DeadLockCheck()
		{
			var warnings = LockChecker.CheckAll("TS3AudioBot", true);
			Assert.IsTrue(warnings.Count == 0, "At least one possible deadlock detected");
		}
	}
}
