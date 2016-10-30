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

namespace TS3Client
{
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Reflection;
	using KVEnu = System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>>;

	static class CommandDeserializer
	{
		// STATIC LOOKUPS

		/// <summary>Maps the name of a notification to the class.</summary>
		private static readonly Dictionary<string, NotifyTypeInfo> NotifyLookup;
		/// <summary>Map of functions to deserialize from query values.</summary>
		private static readonly Dictionary<Type, Func<string, Type, object>> ConvertMap;

		static CommandDeserializer()
		{
			// get all classes deriving from Notification
			var derivedNtfy = from asm in AppDomain.CurrentDomain.GetAssemblies()
							  from type in asm.GetTypes()
							  where type.IsInterface
							  where typeof(INotification).IsAssignableFrom(type)
							  let ntfyAtt = (QueryNotificationAttribute)type.GetCustomAttribute(typeof(QueryNotificationAttribute), false)
							  where ntfyAtt != null
							  select new KeyValuePair<string, NotifyTypeInfo>(ntfyAtt.Name, new NotifyTypeInfo(type, ntfyAtt.NotificationType));
			NotifyLookup = derivedNtfy.ToDictionary(x => x.Key, x => x.Value);

			Util.Init(ref ConvertMap);
			ConvertMap.Add(typeof(bool), (v, t) => v != "0");
			ConvertMap.Add(typeof(sbyte), (v, t) => sbyte.Parse(v, CultureInfo.InvariantCulture));
			ConvertMap.Add(typeof(byte), (v, t) => byte.Parse(v, CultureInfo.InvariantCulture));
			ConvertMap.Add(typeof(short), (v, t) => short.Parse(v, CultureInfo.InvariantCulture));
			ConvertMap.Add(typeof(ushort), (v, t) => ushort.Parse(v, CultureInfo.InvariantCulture));
			ConvertMap.Add(typeof(int), (v, t) => int.Parse(v, CultureInfo.InvariantCulture));
			ConvertMap.Add(typeof(uint), (v, t) => uint.Parse(v, CultureInfo.InvariantCulture));
			ConvertMap.Add(typeof(long), (v, t) => long.Parse(v, CultureInfo.InvariantCulture));
			ConvertMap.Add(typeof(ulong), (v, t) => ulong.Parse(v, CultureInfo.InvariantCulture));
			ConvertMap.Add(typeof(float), (v, t) => float.Parse(v, CultureInfo.InvariantCulture));
			ConvertMap.Add(typeof(double), (v, t) => double.Parse(v, CultureInfo.InvariantCulture));
			ConvertMap.Add(typeof(string), (v, t) => TS3String.Unescape(v));
			ConvertMap.Add(typeof(TimeSpan), (v, t) => TimeSpan.FromSeconds(double.Parse(v, CultureInfo.InvariantCulture)));
			ConvertMap.Add(typeof(DateTime), (v, t) => PrimitiveParameter.UnixTimeStart.AddSeconds(double.Parse(v, CultureInfo.InvariantCulture)));
		}

		// data to error
		public static CommandError GenerateErrorStatus(string line)
		{
			var kvpList = ParseKeyValueLine(line, true);
			var errorStatus = new CommandError();
			foreach (var responseParam in kvpList)
			{
				switch (responseParam.Key.ToUpperInvariant())
				{
					case "ID": errorStatus.Id = int.Parse(responseParam.Value, CultureInfo.InvariantCulture); break;
					case "MSG": errorStatus.Message = TS3String.Unescape(responseParam.Value); break;
					case "FAILED_PERMID": errorStatus.MissingPermissionId = int.Parse(responseParam.Value, CultureInfo.InvariantCulture); break;
					case "RETURN_CODE": errorStatus.ReturnCode = TS3String.Unescape(responseParam.Value); break;
				}
			}
			return errorStatus;
		}

		// data to notification
		public static Tuple<IEnumerable<INotification>, NotificationType> GenerateNotification(string line)
		{
			string notifyname;
			int splitindex = line.IndexOf(' ');
			if (splitindex < 0)
				notifyname = line.TrimEnd();
			else
				notifyname = line.Substring(0, splitindex);

			NotifyTypeInfo targetNotification;
			if (NotifyLookup.TryGetValue(notifyname, out targetNotification))
			{
				string[] messageList;
				if (splitindex < 0)
					messageList = new string[0];
				else
					messageList = line.Substring(splitindex).TrimStart().Split('|');
				return new Tuple<IEnumerable<INotification>, NotificationType>(messageList.Select(msg =>
				{
					var incomingData = ParseKeyValueLine(msg, false);
					var notification = Generator.ActivateNotification(targetNotification.ClassType);
					FillQueryMessage(targetNotification.ClassType, notification, incomingData);
					return notification;
				}), targetNotification.EnumType);
			}
			else
			{
				Debug.WriteLine($"No matching notification derivative ({line})");
				return new Tuple<IEnumerable<INotification>, NotificationType>(Enumerable.Empty<INotification>(), NotificationType.Unknown);
			}
		}

		public static IEnumerable<IResponse> GenerateResponse(string line, Type answerType)
		{
			if (answerType == null)
			{
				if (string.IsNullOrWhiteSpace(line))
					return Enumerable.Empty<ResponseDictionary>();
				var messageList = line.Split('|');
				return messageList.Select(msg => new ResponseDictionary(ParseKeyValueLineDict(msg, false)));
			}
			else
			{
				if (string.IsNullOrWhiteSpace(line))
					return Enumerable.Empty<IResponse>();
				var messageList = line.Split('|');
				return messageList.Select(msg =>
				{
					var incomingData = ParseKeyValueLine(msg, false);
					var response = Generator.ActivateResponse(answerType);
					FillQueryMessage(answerType, response, incomingData);
					return response;
				});
			}
		}

		// HELPER

		private static void FillQueryMessage(Type baseType, IQueryMessage qm, KVEnu kvpData)
		{
			var map = Generator.GetAccessMap(baseType);
			foreach (var kvp in kvpData)
			{
				PropertyInfo prop;
				if (!map.TryGetValue(kvp.Key, out prop))
				{
					Debug.WriteLine($"Missing Parameter '{kvp.Key}' in '{qm}'");
					continue;
				}
				object value = DeserializeValue(kvp.Value, prop.PropertyType);
				prop.SetValue(qm, value);
			}
		}

		private static object DeserializeValue(string data, Type dataType)
		{
			Func<string, Type, object> converter;
			if (ConvertMap.TryGetValue(dataType, out converter))
				return converter(data, dataType);
			else if (dataType.IsEnum)
				return Enum.ToObject(dataType, Convert.ChangeType(data, dataType.GetEnumUnderlyingType(), CultureInfo.InvariantCulture));
			else if (dataType.IsArray)
			{
				Type arrayElementType = dataType.GetElementType();
				var tempArray = data.Split(',').Select(x => DeserializeValue(x, arrayElementType)).ToArray();
				var resultArray = Array.CreateInstance(arrayElementType, tempArray.Length);
				Array.Copy(tempArray, resultArray, tempArray.Length);
				return resultArray;
			}
			else
				throw new NotSupportedException();
		}

		private static KVEnu ParseKeyValueLine(string line, bool ignoreFirst)
		{
			if (string.IsNullOrWhiteSpace(line))
				return Enumerable.Empty<KeyValuePair<string, string>>();
			IEnumerable<string> splitValues = line.Split(' ');
			if (ignoreFirst) splitValues = splitValues.Skip(1);
			return from part in splitValues
				   select part.Split(new[] { '=' }, 2) into keyValuePair
				   select new KeyValuePair<string, string>(keyValuePair[0], keyValuePair.Length > 1 ? keyValuePair[1] : string.Empty);
		}
		private static Dictionary<string, string> ParseKeyValueLineDict(string line, bool ignoreFirst)
			=> ParseKeyValueLineDict(ParseKeyValueLine(line, ignoreFirst));
		private static Dictionary<string, string> ParseKeyValueLineDict(KVEnu data)
			=> data.ToDictionary(pair => pair.Key, pair => pair.Value);

		private sealed class NotifyTypeInfo
		{
			public Type ClassType { get; }
			public NotificationType EnumType { get; }

			public NotifyTypeInfo(Type classType, NotificationType enumType) { ClassType = classType; EnumType = enumType; }
		}
	}
}
