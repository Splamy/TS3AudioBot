// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Messages
{
	using System;
	using System.Collections.Generic;
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
	}
}
