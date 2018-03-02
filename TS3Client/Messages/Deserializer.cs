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
	using Helper;
	using System;
	using System.Collections.Generic;
	using System.Linq;

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
			return ParseKeyValueLine(notification, lineDataPart);
		}

		// data to response
		internal static IEnumerable<T> GenerateResponse<T>(string line) where T : IResponse, new()
		{
			if (string.IsNullOrWhiteSpace(line))
				return Array.Empty<T>();
			var messageList = line.Split('|');
			return messageList.Select(msg => ParseKeyValueLine(new T(), msg)).Where(x => x.Ok).Select(x => x.Value);
		}

		private static R<T> ParseKeyValueLine<T>(T qm, string line) where T : IQueryMessage
		{
			if (string.IsNullOrWhiteSpace(line))
				return R<T>.Err("Empty");

			var ss = new SpanSplitter();
			var lineSpan = ss.First(line, ' ');
			var key = ReadOnlySpan<char>.Empty;
			var value = ReadOnlySpan<char>.Empty;
			try
			{
				do
				{
					var param = ss.Trim(lineSpan);
					var kvpSplitIndex = param.IndexOf('=');
					var skey = kvpSplitIndex >= 0 ? param.Slice(0, kvpSplitIndex) : ReadOnlySpan<char>.Empty;
					value = kvpSplitIndex <= param.Length - 1 ? param.Slice(kvpSplitIndex + 1) : ReadOnlySpan<char>.Empty;

					qm.SetField(skey.NewString(), value);

					if (!ss.HasNext)
						break;
					lineSpan = ss.Next(lineSpan);
				} while (lineSpan.Length > 0);
				return R<T>.OkR(qm);
			}
			catch (Exception ex) { OnError?.Invoke(null, new Error(qm.GetType().Name, line, key.NewString(), value.NewString(), ex)); }
			return R<T>.Err("Error");
		}

		public class Error : EventArgs
		{
			public string Class { get; }
			public string Message { get; }
			public string Field { get; }
			public string Value { get; }
			public Exception Exception { get; }

			public Error(string classname, string message, string field, string value, Exception ex = null) { Class = classname; Message = message; Field = field; Value = value; Exception = ex; }

			public override string ToString() => $"Deserealization format error. Data: class:{Class} field:{Field} value:{Value} msg:{Message}";
		}
	}
}
