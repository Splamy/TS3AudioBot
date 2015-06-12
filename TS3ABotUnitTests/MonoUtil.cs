using Mono.Cecil;
using System;
using System.Linq;
using System.Reflection;

namespace TS3ABotUnitTests
{
	class MonoUtil
	{
		public static AssemblyDefinition GetAsmDefOfType(Type type)
		{
			var asmbly = Assembly.GetAssembly(type);
			return AssemblyDefinition.ReadAssembly(asmbly.Location);
		}

		public static TypeDefinition ReflectionToCecilType(Type type)
		{
			var asmDef = GetAsmDefOfType(type);
			return asmDef.MainModule.Types.FirstOrDefault(t => t.Namespace == type.Namespace && t.Name == type.Name);
		}

		public static object ScanAllTypes(AssemblyDefinition asmDef, Func<TypeDefinition, object> wrong)
		{
			foreach (var type in asmDef.MainModule.Types)
			{
				object result = wrong(type);
				if (result != null)
					return result;
			}
			return null;
		}

		public static object ScanAllMethods(TypeDefinition typDef, Func<MethodDefinition, object> wrong)
		{
			foreach (var method in typDef.Methods)
			{
				object result = wrong(method);
				if (result != null)
					return result;
			}
			return null;
		}
	}
}
