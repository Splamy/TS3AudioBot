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
			var warnings = LockChecker.CheckAll<MainBot>(true);
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

							// if its not a task we dont want it
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
			string[] values = new string[] { "val1", "val2", "val3", "val4", "val5" };
			int adix = 0;

			trie.Add("hans", values[adix++]);
			Assert.AreEqual(string.Format("+(h*(a*(n*(s[{0}]))))", values), trie.ToString());
			trie.Add("hani", values[adix++]);
			Assert.AreEqual(string.Format("+(h(a(n(i[{1}]s[{0}]))))", values), trie.ToString());
			trie.Add("hana", values[adix++]);
			Assert.AreEqual(string.Format("+(h(a(n(a[{2}]i[{1}]s[{0}]))))", values), trie.ToString());
			trie.Add("hansolo", values[adix++]);
			Assert.AreEqual(string.Format("+(h(a(n(a[{2}]i[{1}]s[{0}](o*(l*(o[{3}])))))))", values), trie.ToString());
			trie.Add("hansololo", values[adix++]);
			Assert.AreEqual(string.Format("+(h(a(n(a[{2}]i[{1}]s[{0}](o(l(o[{3}](l*(o[{4}])))))))))", values), trie.ToString());
		}
	}
}
