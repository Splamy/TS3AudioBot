using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using LockCheck.Internal;

namespace LockCheck
{
	public sealed class LockChecker
	{
		private string nameSpace;
		private List<LockCriticalWarning> warningList;
		private HashSet<MethodInfo> checkedList;
		private List<MethodInfo> criticalMethods;
		private Stack<MethodInfo> callHierarchy;

		private LockChecker(string nameSpace = null)
		{
			this.nameSpace = nameSpace;
			warningList = new List<LockCriticalWarning>();
			checkedList = new HashSet<MethodInfo>();
			criticalMethods = new List<MethodInfo>();
			callHierarchy = new Stack<MethodInfo>();
		}

		/// <summary>Check all <see cref="LockCriticalAttribute"/> marked methods in a class</summary>
		/// <typeparam name="T">The class to check</typeparam>
		/// <param name="print">True if the warnings should be printed to Console</param>
		/// <returns>Returns a list of all possible deadlock calls</returns>
		public static IReadOnlyList<LockCriticalWarning> Check<T>(bool print = false)
		{
			return DoCheck(lc => lc.CheckInternal(typeof(T)), null, print);
		}

		/// <summary>Check all <see cref="LockCriticalAttribute"/> marked methods in a Namespace</summary>
		/// <param name="nameSpace">The namespace containing all classes to be checked</param>
		/// <param name="print">True if the warnings should be printed to Console</param>
		/// <returns>Returns a list of all possible deadlock calls</returns>
		public static IReadOnlyList<LockCriticalWarning> CheckAll(string nameSpace, bool print = false)
		{
			return DoCheck((lc => lc.CheckAllInternal()), nameSpace, print);
		}

		private static IReadOnlyList<LockCriticalWarning> DoCheck(Action<LockChecker> checkCall, string nameSpace, bool print)
		{
			LockChecker lc = new LockChecker(nameSpace);
			checkCall(lc);
			if (print)
			{
				foreach (var warning in lc.warningList)
				{
					Console.WriteLine(warning.Description);
				}
			}
			return lc.warningList.AsReadOnly();
		}

		private void CheckInternal(Type checkType)
		{
			GetMarkedMethods(checkType);
			foreach (MethodInfo criticalMethod in criticalMethods)
			{
				checkedList.Clear();
				callHierarchy.Push(criticalMethod);

				while (callHierarchy.Count > 0)
				{
					var instructions = MethodBodyReader.GetInstructions(callHierarchy.Peek());
					bool foundNewCall = false;
					foreach (Instruction instruction in instructions)
					{
						// Check if instruction is a call
						MethodInfo callMethod = instruction.Operand as MethodInfo;
						if (callMethod == null) continue;

						// Check if we already checked the method
						if (checkedList.Contains(callMethod)) continue;

						// We don't want to check method calls out of the class
						if (callMethod.DeclaringType != criticalMethod.DeclaringType) continue;

						checkedList.Add(callMethod);
						callHierarchy.Push(callMethod);
						AddInterferences(criticalMethod, callMethod);
						foundNewCall = true;
						break;
					}
					if (!foundNewCall)
						callHierarchy.Pop();
				}
			}
		}

		private void CheckAllInternal()
		{
			Type[] typelist = GetTypesInNamespace(nameSpace);
			foreach (Type type in typelist)
			{
				CheckInternal(type);
			}
		}

		private void AddInterferences(MethodInfo methodCalling, MethodInfo methodCalled)
		{
			LockCriticalAttribute lcaCalling = methodCalling.GetCustomAttribute<LockCriticalAttribute>();
			if (lcaCalling == null) return;
			LockCriticalAttribute lcaCalled = methodCalled.GetCustomAttribute<LockCriticalAttribute>();
			if (lcaCalled == null) return;
			IList<string> intersections = lcaCalling.LocksUsed.Intersect<string>(lcaCalled.LocksUsed).ToList();
			foreach (string intersection in intersections)
			{
				StringBuilder strb = new StringBuilder();
				foreach (var mi in callHierarchy.Reverse().ToList())
					strb.AppendLine(FormatMethod(mi));
				warningList.Add(new LockCriticalWarning(methodCalling, methodCalled, intersection, strb.ToString()));
			}
		}

		private string FormatMethod(MethodInfo mi)
		{
			StringBuilder strb = new StringBuilder();
			var parameters = mi.GetParameters().Select(p => p.ParameterType.FullName + " " + p.Name).ToArray();

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

		private void GetMarkedMethods(Type type)
		{
			criticalMethods.Clear();
			foreach (MethodInfo mi in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
			{
				if (mi.GetCustomAttribute<LockCriticalAttribute>() != null)
				{
					criticalMethods.Add(mi);
				}
			}
		}

		private Type[] GetTypesInNamespace(string nameSpace)
		{
			return Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace == nameSpace).ToArray();
		}
	}

	public sealed class LockCriticalWarning
	{
		public MethodInfo MethodCalling { get; private set; }
		public MethodInfo MethodCalled { get; private set; }
		public string LockName { get; private set; }
		public string Description { get; private set; }

		public LockCriticalWarning(MethodInfo methodCalling, MethodInfo methodCalled, string lockName, string callStack)
		{
			MethodCalling = methodCalling;
			MethodCalled = methodCalled;
			LockName = lockName;
			Description = string.Format("The method call hierarchy:\n{0}might cause a deadlock due to the lock of \"{1}\"", callStack, lockName);
		}
	}
}
