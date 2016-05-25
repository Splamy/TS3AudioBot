// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.


namespace TS3ABotUnitTests
{
	using Mono.Cecil;
	using System;
	using System.Linq;
	using System.Reflection;

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
