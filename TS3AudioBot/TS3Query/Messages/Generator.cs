namespace TS3Query.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using System.Reflection.Emit;
	using MapTarget = System.Reflection.PropertyInfo;

	public static class Generator
	{
		private static readonly Dictionary<Type, InitializerData> generatedTypes;
		private static readonly AssemblyName GenAssemblyName = new AssemblyName("QueryMessages");
		private static readonly AssemblyBuilder GenAssemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(GenAssemblyName, AssemblyBuilderAccess.Run);
		private static readonly ModuleBuilder GenModuleBuilder = GenAssemblyBuilder.DefineDynamicModule("MainModule");
		private const MethodAttributes PropMethods = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual;

		static Generator()
		{
			Helper.Init(ref generatedTypes);
		}

		public static T ActivateNotification<T>() where T : INotification => (T)ActivateNotification(typeof(T));
		public static INotification ActivateNotification(Type t) => (INotification)Activate(t, true);

		public static T ActivateResponse<T>() where T : IResponse => (T)ActivateResponse(typeof(T));
		public static IResponse ActivateResponse(Type t) => (IResponse)Activate(t, false);

		public static Dictionary<string, MapTarget> GetAccessMap(Type t) => generatedTypes[t].AccessMap;

		private static object Activate(Type backingInterface, bool notifyProp)
		{
			InitializerData genType;
			if (!generatedTypes.TryGetValue(backingInterface, out genType))
			{
				genType = Generate(backingInterface, notifyProp);
				generatedTypes.Add(backingInterface, genType);
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
			foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
				yield return prop;

			foreach (var iface in t.GetInterfaces())
				foreach (var prop in GetPropertyRequestsRecursive(iface))
					yield return prop;
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
