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
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using KVEnu = System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>>;

	static class CommandDeserializer
	{
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
				case "MSG": errorStatus.Message = Ts3String.Unescape(responseParam.Value); break;
				case "FAILED_PERMID": errorStatus.MissingPermissionId = int.Parse(responseParam.Value, CultureInfo.InvariantCulture); break;
				case "RETURN_CODE": errorStatus.ReturnCode = Ts3String.Unescape(responseParam.Value); break;
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

			var ntfyType = MessageHelper.GetNotificationType(notifyname);
			if (ntfyType != NotificationType.Unknown)
			{
				string[] messageList;
				if (splitindex < 0)
					messageList = new string[0];
				else
					messageList = line.Substring(splitindex).TrimStart().Split('|');
				return new Tuple<IEnumerable<INotification>, NotificationType>(messageList.Select(msg =>
				{
					var incomingData = ParseKeyValueLine(msg, false);
					var notification = MessageHelper.GenerateNotificationType(ntfyType);
					FillQueryMessage(notification, incomingData);
					return notification;
				}), ntfyType);
			}
			else
			{
				Debug.WriteLine($"No matching notification derivative ({line})");
				return new Tuple<IEnumerable<INotification>, NotificationType>(Enumerable.Empty<INotification>(), NotificationType.Unknown);
			}
		}

		// data to response
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
					var response = (IResponse)Activator.CreateInstance(answerType);
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
		public static DateTime DeserializeDateTime(string v) => PrimitiveParameter.UnixTimeStart.AddSeconds(double.Parse(v, CultureInfo.InvariantCulture));
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
