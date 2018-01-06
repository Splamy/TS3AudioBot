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

	public static class Deserializer
	{
		public static event EventHandler<Error> OnError;

		// data to notification
		internal static IEnumerable<INotification> GenerateNotification(string lineDataPart, NotificationType ntfyType)
		{
			if (ntfyType == NotificationType.Unknown)
				throw new ArgumentException("The NotificationType must not be unknown", nameof(lineDataPart));

			if (lineDataPart == null)
				throw new ArgumentNullException(nameof(lineDataPart));

			return lineDataPart.TrimStart().Split('|').Select(msg => GenerateSingleNotification(msg, ntfyType)).Where(x => x.Ok).Select(x => x.Value);
		}

		internal static R<INotification> GenerateSingleNotification(string lineDataPart, NotificationType ntfyType)
		{
			if (ntfyType == NotificationType.Unknown)
				throw new ArgumentException("The NotificationType must not be unknown", nameof(lineDataPart));

			if (lineDataPart == null)
				throw new ArgumentNullException(nameof(lineDataPart));

			var notification = MessageHelper.GenerateNotificationType(ntfyType);
			return ParseKeyValueLine(notification, lineDataPart, false);
		}

		// data to response
		internal static IEnumerable<T> GenerateResponse<T>(string line) where T : IResponse, new()
		{
			if (string.IsNullOrWhiteSpace(line))
				return Enumerable.Empty<T>();
			var messageList = line.Split('|');
			return messageList.Select(msg => ParseKeyValueLine(new T(), msg, false)).Where(x => x.Ok).Select(x => x.Value);
		}

		private static R<T> ParseKeyValueLine<T>(T qm, string line, bool ignoreFirst) where T : IQueryMessage
		{
			if (string.IsNullOrWhiteSpace(line))
				return R<T>.Err("Empty");

			// can be optimized by using Span (wating for more features)
			var splitValues = line.Split(' ');
			string key = null, value = null;
			try
			{
				for (int i = ignoreFirst ? 1 : 0; i < splitValues.Length; i++)
				{
					var keyValuePair = splitValues[i].Split(new[] { '=' }, 2);
					key = keyValuePair[0];
					value = keyValuePair.Length > 1 ? keyValuePair[1] : string.Empty;
					qm.SetField(key, value);
				}
				return R<T>.OkR(qm);
			}
			catch (Exception) { OnError?.Invoke(null, new Error(qm.GetType().Name, line, key, value)); }
			return R<T>.Err("Error");
		}

		public class Error : EventArgs
		{
			public string Class { get; set; }
			public string Message { get; set; }
			public string Field { get; }
			public string Value { get; }

			public Error(string classname, string message, string field, string value) { Class = classname; Message = message; Field = field; Value = value; }

			public override string ToString() => $"Deserealization format error. Data: class:{Class} field:{Field} value:{Value} msg:{Message}";
		}
	}
}
