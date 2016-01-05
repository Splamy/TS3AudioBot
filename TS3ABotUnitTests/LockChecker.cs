using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LockCheck
{
	public sealed class LockChecker
	{
		private static OpCode[][] MONITOR_ENTER_1 = new[] { new[] { OpCodes.Ldfld, OpCodes.Ldsfld } };
		private static OpCode[][] MONITOR_ENTER_2 = new[] {
			new[] { OpCodes.Ldloca_S, OpCodes.Ldloca },
			new[] { OpCodes.Stloc_0, OpCodes.Stloc_S, OpCodes.Stloc, OpCodes.Stloc_1, OpCodes.Stloc_2, OpCodes.Stloc_3 } ,
			new[] { OpCodes.Dup },
			new[] { OpCodes.Ldfld, OpCodes.Ldsfld } };

		private List<LockCriticalWarning> warningList;
		private HashSet<MethodDefinition> checkedList;
		private List<LockCritialData> criticalMethods;
		private Stack<MethodDefinition> callHierarchy;

		private LockChecker()
		{
			warningList = new List<LockCriticalWarning>();
			checkedList = new HashSet<MethodDefinition>();
			criticalMethods = new List<LockCritialData>();
			callHierarchy = new Stack<MethodDefinition>();
		}

		/// <summary>Check all <see cref="LockCriticalAttribute"/> marked methods in a class</summary>
		/// <typeparam name="T">The class to check</typeparam>
		/// <param name="print">True if the warnings should be printed to Console</param>
		/// <returns>Returns a list of all possible deadlock calls</returns>
		public static IReadOnlyList<LockCriticalWarning> Check<T>(bool print)
		{
			return DoCheck(typeof(T), false, print);
		}

		/// <summary>Check all <see cref="LockCriticalAttribute"/> marked methods in a Namespace</summary>
		/// <param name="nameSpace">The namespace containing all classes to be checked</param>
		/// <param name="print">True if the warnings should be printed to Console</param>
		/// <returns>Returns a list of all possible deadlock calls</returns>
		public static IReadOnlyList<LockCriticalWarning> CheckAll<T>(bool print)
		{
			return DoCheck(typeof(T), true, print);
		}

		private static IReadOnlyList<LockCriticalWarning> DoCheck(Type type, bool entireModule, bool print)
		{
			LockChecker lc = new LockChecker();
			if (entireModule)
			{
				lc.CheckAllInternal(type);
			}
			else
			{
				TypeDefinition typDef = ReflectionToCecilType(type);
				lc.CheckInternal(typDef);
			}
			if (print)
			{
				foreach (var warning in lc.warningList)
				{
					Console.WriteLine(warning.Description);
				}
			}
			return lc.warningList.AsReadOnly();
		}

		private void CheckInternal(TypeDefinition checkType)
		{
			GetCriticalMethods(checkType);
			foreach (LockCritialData lockCritialCaller in criticalMethods)
			{
				checkedList.Clear();
				MethodDefinition criticalMethod = lockCritialCaller.method;
				callHierarchy.Push(criticalMethod);

				while (callHierarchy.Count > 0)
				{
					MethodDefinition currentMethod = callHierarchy.Peek() as MethodDefinition;
					if (currentMethod != null && !currentMethod.HasBody)
						continue;
					bool foundNewCall = false;
					foreach (var instruction in currentMethod.Body.Instructions)
					{
						if (instruction.OpCode != OpCodes.Call)
							continue;

						// Check if instruction is a call
						MethodDefinition callMethod = instruction.Operand as MethodDefinition;
						if (callMethod == null) continue;

						// Check if we already checked the method
						if (checkedList.Contains(callMethod)) continue;

						// We don't want to check method calls out of the class
						if (callMethod.DeclaringType != criticalMethod.DeclaringType) continue;

						checkedList.Add(callMethod);
						callHierarchy.Push(callMethod);

						var lockCritialCalled = criticalMethods.FirstOrDefault(m => m.method == callMethod);
						if (lockCritialCalled != null)
							AddInterferences(lockCritialCaller, lockCritialCalled);
						foundNewCall = true;
						break;
					}
					if (!foundNewCall)
						callHierarchy.Pop();
				}
			}
		}

		private void CheckAllInternal(Type typeOfModule)
		{
			var asmDef = GetAsmDefOfType(typeOfModule);
			foreach (TypeDefinition type in asmDef.MainModule.Types)
			{
				CheckInternal(type);
			}
		}

		private void AddInterferences(LockCritialData methodCalling, LockCritialData methodCalled)
		{
			var intersections = methodCalling.lockObjects.Intersect(methodCalled.lockObjects).ToList();
			foreach (MemberReference intersection in intersections)
			{
				StringBuilder strb = new StringBuilder();
				foreach (var mi in callHierarchy.Reverse().ToList())
					strb.AppendLine(FormatMethod(mi));
				warningList.Add(new LockCriticalWarning(intersection.Name, strb.ToString()));
			}
		}

		private string FormatMethod(MethodReference mi)
		{
			StringBuilder strb = new StringBuilder();
			var parameters = mi.Parameters.Select(p => p.ParameterType.FullName + " " + p.Name).ToArray();
			strb.Append(mi.DeclaringType.FullName);
			strb.Append('.');
			strb.Append(mi.Name);
			strb.Append('(');
			for (int i = 0; i < parameters.Length; i++)
			{
				strb.Append(parameters[i]);
				if (i < parameters.Length - 1) strb.Append(", ");
			}
			strb.Append(");");
			return strb.ToString();
		}

		private void GetCriticalMethods(TypeDefinition typDef)
		{
			criticalMethods.Clear();

			foreach (var method in typDef.Methods)
			{
				if (!method.HasBody)
					continue;

				LockCritialData lcd = null;

				foreach (var instruction in method.Body.Instructions)
				{
					// we only search calls, Monitor calls to be precise
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
					if (typRefCalled.FullName != "System.Threading.Monitor")
						continue;

					if (metDefCalled.Name == "Enter")
					{
						if (lcd == null) lcd = new LockCritialData(method);
						MemberReference memref = GetPushStackObj(instruction, metDefCalled.Parameters.Count == 1 ? MONITOR_ENTER_1 : MONITOR_ENTER_2);
						if (memref != null)
							lcd.lockObjects.Add(memref);
						else
							lcd.isAnyMonitor = true;
					}
				}

				if (lcd != null)
					criticalMethods.Add(lcd);
			}
		}

		private MemberReference GetPushStackObj(Instruction start, OpCode[][] reverseInstructions)
		{
			Instruction current = start;
			foreach (var opc in reverseInstructions)
			{
				if (current.Previous == null)
					return null;
				current = current.Previous;
				if (!opc.Contains(current.OpCode))
					return null;
			}
			return current.Operand as MemberReference;
		}

		private static AssemblyDefinition GetAsmDefOfType(Type type)
		{
			var asmbly = Assembly.GetAssembly(type);
			return AssemblyDefinition.ReadAssembly(asmbly.Location);
		}

		private static TypeDefinition ReflectionToCecilType(Type type)
		{
			var asmDef = GetAsmDefOfType(type);
			return asmDef.MainModule.Types.FirstOrDefault(t => t.Namespace == type.Namespace && t.Name == type.Name);
		}

		private class LockCritialData
		{
			public MethodDefinition method;
			public List<MemberReference> lockObjects;
			public bool isAnyMonitor;

			public LockCritialData(MethodDefinition method)
			{
				this.method = method;
				lockObjects = new List<MemberReference>();
			}
		}
	}

	public sealed class LockCriticalWarning
	{
		public string LockName { get; private set; }
		public string Description { get; private set; }

		public LockCriticalWarning(string lockName, string callStack)
		{
			LockName = lockName;
			Description = string.Format("The method call hierarchy:\n{0}might cause a deadlock due to the lock of \"{1}\"", callStack, lockName);
		}
	}
}
