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
		private static Dictionary<string, Type> notifyLookup;
		/// <summary>Map of functions to deserialize from query values.</summary>
		private static Dictionary<Type, Func<string, Type, object>> convertMap;

		static CommandDeserializer()
		{
			// get all classes deriving from Notification
			var derivedNtfy = from asm in AppDomain.CurrentDomain.GetAssemblies()
							  from type in asm.GetTypes()
							  where type.IsInterface
							  where typeof(INotification).IsAssignableFrom(type)
							  let ntfyAtt = type.GetCustomAttribute(typeof(QueryNotificationAttribute), false)
							  where ntfyAtt != null
							  select new KeyValuePair<string, Type>(((QueryNotificationAttribute)ntfyAtt).Name, type);
			notifyLookup = derivedNtfy.ToDictionary(x => x.Key, x => x.Value);

			Helper.Init(ref convertMap);
			convertMap.Add(typeof(bool), (v, t) => v != "0");
			convertMap.Add(typeof(sbyte), (v, t) => sbyte.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(byte), (v, t) => byte.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(short), (v, t) => short.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(ushort), (v, t) => ushort.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(int), (v, t) => int.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(uint), (v, t) => uint.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(long), (v, t) => long.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(ulong), (v, t) => ulong.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(float), (v, t) => float.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(double), (v, t) => double.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(string), (v, t) => TS3String.Unescape(v));
			convertMap.Add(typeof(TimeSpan), (v, t) => TimeSpan.FromSeconds(double.Parse(v, CultureInfo.InvariantCulture)));
			convertMap.Add(typeof(DateTime), (v, t) => PrimitiveParameter.unixTimeStart.AddSeconds(double.Parse(v, CultureInfo.InvariantCulture)));
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
				}
			}
			return errorStatus;
		}

		// data to notification
		public static INotification GenerateNotification(string line)
		{
			int splitindex = line.IndexOf(' ');
			if (splitindex < 0) throw new ArgumentException("line couldn't be parsed");
			Type targetNotification;
			string notifyname = line.Substring(0, splitindex);
			if (notifyLookup.TryGetValue(notifyname, out targetNotification))
			{
				var notification = Generator.ActivateNotification(targetNotification);
				var incommingData = ParseKeyValueLine(line, true);
				FillQueryMessage(targetNotification, notification, incommingData);
				return notification;
			}
			else throw new NotSupportedException("No matching notification derivative");
		}

		public static IEnumerable<IResponse> GenerateResponse(string line, Type answerType)
		{
			/*if (!requestQueue.Any())
				throw new InvalidOperationException();

			var peekResponse = requestQueue.Peek();*/

			var messageList = line?.Split('|');
			if (answerType == null)
			{
				if (string.IsNullOrWhiteSpace(line))
					return Enumerable.Empty<ResponseDictionary>();
				return messageList.Select(msg => new ResponseDictionary(ParseKeyValueLineDict(msg, false)));
			}
			else
			{
				if (string.IsNullOrWhiteSpace(line))
					return Enumerable.Empty<IResponse>();
				return messageList.Select(msg =>
				{
					var response = Generator.ActivateResponse(answerType);
					FillQueryMessage(answerType, response, ParseKeyValueLine(msg, false));
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
					Debug.Write($"Missing Parameter '{kvp.Key}' in '{qm}'");
					continue;
				}
				object value = DeserializeValue(kvp.Value, prop.PropertyType);
				prop.SetValue(qm, value);
			}
		}

		private static object DeserializeValue(string data, Type dataType)
		{
			Func<string, Type, object> converter;
			if (convertMap.TryGetValue(dataType, out converter))
				return converter(data, dataType);
			else if (dataType.IsEnum)
				return Enum.ToObject(dataType, Convert.ChangeType(data, dataType.GetEnumUnderlyingType(), CultureInfo.InvariantCulture));
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
		private static IDictionary<string, string> ParseKeyValueLineDict(string line, bool ignoreFirst)
			=> ParseKeyValueLineDict(ParseKeyValueLine(line, ignoreFirst));
		private static IDictionary<string, string> ParseKeyValueLineDict(KVEnu data)
			=> data.ToDictionary(pair => pair.Key, pair => pair.Value);
	}
}
