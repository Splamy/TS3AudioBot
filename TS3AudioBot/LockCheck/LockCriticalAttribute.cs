using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LockCheck
{
	/// <summary>
	/// Detects possbile obvious deadlocks caused by a call hierarchie with at least two methods locking the same object.<para/>
	/// To get the evaluation, call the <see cref="LockChecker.Check(bool)"/> or <see cref="LockChecker.CheckAll(string, bool)"/> method from the <see cref="LockChecker"/>
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	class LockCriticalAttribute : Attribute
	{
		public IReadOnlyCollection<string> LocksUsed { get; private set; }

		/// <summary>Saves all locked objects</summary>
		/// <param name="lockName">All object names locked by this method</param>
		public LockCriticalAttribute(params string[] lockName)
		{
			LocksUsed = Array.AsReadOnly<string>(lockName);
		}
	}
}
