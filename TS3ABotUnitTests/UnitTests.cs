using LockCheck;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using System;
using TS3AudioBot;

namespace TS3ABotUnitTests
{
	[TestFixture]
	public class UnitTests
	{
		[Test]
		public void DeadLockCheck()
		{
			var warnings = LockChecker.CheckAll<TS3AudioBot.MainBot>(true);
			Assert.IsTrue(warnings.Count == 0, "At least one possible deadlock detected");
		}

		[Test]
		public void AsyncResultMustNotBeUsed()
		{
			var asmDef = MonoUtil.GetAsmDefOfType(typeof(TS3AudioBot.MainBot));

			object firstresult = MonoUtil.ScanAllTypes(asmDef,
				(type) => MonoUtil.ScanAllMethods(type,
					(method) =>
					{
						if (!method.HasBody)
							return null;
						foreach (var instruction in method.Body.Instructions)
						{
							if (instruction.OpCode != OpCodes.Call)
								continue;

							// see if we can resolve the call
							MethodReference metDefCalled = instruction.Operand as MethodReference;
							if (metDefCalled == null)
								continue;

							// see if we can resolve the declaring type of the call
							TypeReference typRefCalled = metDefCalled.DeclaringType;
							if (typRefCalled == null)
								continue;

							// if its not a monitor we dont want it
							if (!typRefCalled.FullName.StartsWith("System.Threading.Tasks.Task`1"))
								continue;

							if (metDefCalled.Name == "get_Result")
								return method;
						}
						return null;
					}));
			Assert.IsNull(firstresult, "Task.Result must not be used!");
		}

		[Test]
		public void TrieStructureTests()
		{
			Trie<string> trie = new Trie<string>();
			trie.Add("hans", "val1");
			Assert.True(trie.ToString() == "+(h*(a*(n*(s[val]))))")
		}
	}
}
