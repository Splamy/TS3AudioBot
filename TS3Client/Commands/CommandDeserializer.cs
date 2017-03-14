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

namespace TS3Client.Commands
{
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using KVEnu = System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>>;

	internal static class CommandDeserializer
	{
		// data to notification
		public static IEnumerable<INotification> GenerateNotification(string lineDataPart, NotificationType ntfyType)
		{
			if (ntfyType == NotificationType.Unknown)
				throw new ArgumentException("The NotificationType must not be unknown", nameof(lineDataPart));

			if (lineDataPart == null)
				throw new ArgumentNullException(nameof(lineDataPart));

			return lineDataPart.TrimStart().Split('|').Select(msg => GenerateSingleNotification(msg, ntfyType));
		}

		public static INotification GenerateSingleNotification(string lineDataPart, NotificationType ntfyType)
		{
			if (ntfyType == NotificationType.Unknown)
				throw new ArgumentException("The NotificationType must not be unknown", nameof(lineDataPart));

			if (lineDataPart == null)
				return null;

			var incomingData = ParseKeyValueLine(lineDataPart, false);
			var notification = MessageHelper.GenerateNotificationType(ntfyType);
			FillQueryMessage(notification, incomingData);
			return notification;
		}

		// data to response
		public static IEnumerable<T> GenerateResponse<T>(string line) where T : IResponse, new()
		{
			if (typeof(T) == typeof(ResponseDictionary))
			{
				if (string.IsNullOrWhiteSpace(line))
					return Enumerable.Empty<T>();
				var messageList = line.Split('|');
				return (IEnumerable<T>)messageList.Select(msg => new ResponseDictionary(ParseKeyValueLineDict(msg, false)));
			}
			else
			{
				if (string.IsNullOrWhiteSpace(line))
					return Enumerable.Empty<T>();
				var messageList = line.Split('|');
				return messageList.Select(msg =>
				{
					var incomingData = ParseKeyValueLine(msg, false);
					var response = new T();
					FillQueryMessage(response, incomingData);
					return response;
				});
			}
		}

		// HELPER

		private static void FillQueryMessage(IQueryMessage qm, KVEnu kvpData)
		{
			foreach (var kvp in kvpData)
			{
				qm.SetField(kvp.Key, kvp.Value);
			}
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

		public static bool DeserializeBool(string v) => v != "0";
		public static sbyte DeserializeInt8(string v) => sbyte.Parse(v, CultureInfo.InvariantCulture);
		public static byte DeserializeUInt8(string v) => byte.Parse(v, CultureInfo.InvariantCulture);
		public static short DeserializeInt16(string v) => short.Parse(v, CultureInfo.InvariantCulture);
		public static ushort DeserializeUInt16(string v) => ushort.Parse(v, CultureInfo.InvariantCulture);
		public static int DeserializeInt32(string v) => int.Parse(v, CultureInfo.InvariantCulture);
		public static uint DeserializeUInt32(string v) => uint.Parse(v, CultureInfo.InvariantCulture);
		public static long DeserializeInt64(string v) => long.Parse(v, CultureInfo.InvariantCulture);
		public static ulong DeserializeUInt64(string v) => ulong.Parse(v, CultureInfo.InvariantCulture);
		public static float DeserializeSingle(string v) => float.Parse(v, CultureInfo.InvariantCulture);
		public static double DeserializeDouble(string v) => double.Parse(v, CultureInfo.InvariantCulture);
		public static string DeserializeString(string v) => Ts3String.Unescape(v);
		public static TimeSpan DeserializeTimeSpan(string v) => TimeSpan.FromSeconds(double.Parse(v, CultureInfo.InvariantCulture));
		public static DateTime DeserializeDateTime(string v) => Util.UnixTimeStart.AddSeconds(double.Parse(v, CultureInfo.InvariantCulture));
		public static T DeserializeEnum<T>(string v) where T : struct
		{
			T val;
			if (!Enum.TryParse(v, out val))
				throw new FormatException();
			return val;
		}
		public static T[] DeserializeArray<T>(string v, Func<string, T> converter)
			=> v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Select(converter).ToArray();
	}
}
