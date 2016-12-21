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

namespace TS3Client.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using System.Reflection.Emit;
	using MapTarget = System.Reflection.PropertyInfo;

	public static class Generator
	{
		private static readonly Dictionary<Type, InitializerData> GeneratedTypes;
		private static readonly AssemblyName GenAssemblyName = new AssemblyName("QueryMessages");
		private static readonly AssemblyBuilder GenAssemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(GenAssemblyName, AssemblyBuilderAccess.Run);
		private static readonly ModuleBuilder GenModuleBuilder = GenAssemblyBuilder.DefineDynamicModule("MainModule");
		private const MethodAttributes PropMethods = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual;
		private static readonly object typeMapLock = new object();

		static Generator()
		{
			Util.Init(ref GeneratedTypes);
		}

		public static T ActivateNotification<T>() where T : INotification => (T)ActivateNotification(typeof(T));
		public static INotification ActivateNotification(Type t) => (INotification)Activate(t, true);

		public static T ActivateResponse<T>() where T : IResponse => (T)ActivateResponse(typeof(T));
		public static IResponse ActivateResponse(Type t) => (IResponse)Activate(t, false);

		public static Dictionary<string, MapTarget> GetAccessMap(Type t) { lock (typeMapLock) { return GeneratedTypes[t].AccessMap; } }

		private static object Activate(Type backingInterface, bool notifyProp)
		{
			InitializerData genType;
			lock(typeMapLock)
			{
				if (!GeneratedTypes.TryGetValue(backingInterface, out genType))
				{
					genType = Generate(backingInterface, notifyProp);
					GeneratedTypes.Add(backingInterface, genType);
				}
			}
			return Activator.CreateInstance(genType.ActivationType);
		}

		private static InitializerData Generate(Type backingInterface, bool notifyProp)
		{
			QueryNotificationAttribute qna = null;
			if (notifyProp)
			{
				qna = backingInterface.GetCustomAttribute<QueryNotificationAttribute>();
				if (qna == null) throw new ArgumentException("Notification has no QueryNotificationAttribute");
			}

			TypeBuilder tb = GenModuleBuilder.DefineType(
				"C" + backingInterface.Name,
				TypeAttributes.Public |
				TypeAttributes.AutoClass |
				TypeAttributes.BeforeFieldInit,
				null);
			tb.AddInterfaceImplementation(backingInterface);

			var accessMap = new Dictionary<string, MapTarget>();

			foreach (var propRequest in GetPropertyRequestsRecursive(backingInterface))
			{
				if (!propRequest.CanWrite || !propRequest.CanRead) continue;
				var qsa = propRequest.GetCustomAttribute<QuerySerializedAttribute>();
				if (qsa == null) continue;

				var PropertyType = propRequest.PropertyType;

				var backingField = tb.DefineField(
					"fld_" + propRequest.Name,
					PropertyType,
					FieldAttributes.Private);

				var getMethod = tb.DefineMethod(
					propRequest.GetMethod.Name,
					PropMethods,
					PropertyType,
					Type.EmptyTypes);
				var ilGen = getMethod.GetILGenerator();
				ilGen.Emit(OpCodes.Ldarg_0);
				ilGen.Emit(OpCodes.Ldfld, backingField);
				ilGen.Emit(OpCodes.Ret);

				var setMethod = tb.DefineMethod(
					propRequest.SetMethod.Name,
					PropMethods,
					null,
					new[] { PropertyType });
				ilGen = setMethod.GetILGenerator();
				ilGen.Emit(OpCodes.Ldarg_0);
				ilGen.Emit(OpCodes.Ldarg_1);
				ilGen.Emit(OpCodes.Stfld, backingField);
				ilGen.Emit(OpCodes.Ret);

				tb.DefineMethodOverride(getMethod, propRequest.GetMethod);
				tb.DefineMethodOverride(setMethod, propRequest.SetMethod);

				accessMap.Add(qsa.Name, propRequest);
			}

			if (notifyProp)
			{
				var iGetProperty = typeof(INotification).GetProperty(nameof(INotification.NotifyType));

				var getMethod = tb.DefineMethod(
					iGetProperty.GetMethod.Name,
					PropMethods,
					typeof(NotificationType),
					Type.EmptyTypes);
				var ilGen = getMethod.GetILGenerator();
				ilGen.Emit(OpCodes.Ldc_I4, (int)qna.NotificationType);
				ilGen.Emit(OpCodes.Ret);

				tb.DefineMethodOverride(getMethod, iGetProperty.GetMethod);
			}

			return new InitializerData(tb.CreateType(), accessMap);
		}

		private static IEnumerable<MapTarget> GetPropertyRequestsRecursive(Type t)
		{
			var metahashTable = new HashSet<int>();

			foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (metahashTable.Contains(prop.MetadataToken)) continue;
				metahashTable.Add(prop.MetadataToken);
				yield return prop;
			}

			foreach (var iface in t.GetInterfaces())
				foreach (var prop in GetPropertyRequestsRecursive(iface))
				{
					if (metahashTable.Contains(prop.MetadataToken)) continue;
					metahashTable.Add(prop.MetadataToken);
					yield return prop;
				}
		}
	}

	class InitializerData
	{
		public readonly Type ActivationType;
		public readonly Dictionary<string, MapTarget> AccessMap;

		public InitializerData(Type type, Dictionary<string, MapTarget> accessMap)
		{
			ActivationType = type;
			AccessMap = accessMap;
		}
	}
}
