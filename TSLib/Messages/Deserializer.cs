// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using TSLib.Helper;

namespace TSLib.Messages
{
	public class Deserializer
	{
		protected static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		public IPermissionTransform PermissionTransform { get; set; } = DummyPermissionTransform.Instance;

		private const byte AsciiSpace = (byte)' ';
		private const byte AsciiPipe = (byte)'|';
		private const byte AsciiEquals = (byte)'=';

		// data to notification
		public R<INotification[]> GenerateNotification(ReadOnlySpan<byte> line, NotificationType ntfyType)
		{
			if (ntfyType == NotificationType.Unknown)
				throw new ArgumentException("The NotificationType must not be unknown", nameof(ntfyType));

			var pipes = PipeList(line);
			var arr = MessageHelper.InstatiateNotificationArray(ntfyType, (pipes?.Count ?? 0) + 1);
			return Dersialize(arr, line, pipes);
		}

		public R<INotification> GenerateSingleNotification(ReadOnlySpan<byte> line, NotificationType ntfyType)
		{
			if (line.IsEmpty)
				throw new ArgumentNullException(nameof(line));

			var result = GenerateNotification(line, ntfyType);
			if (!result.Ok || result.Value.Length == 0)
				return R.Err;
			return R<INotification>.OkR(result.Value[0]);
		}

		private static List<int> PipeList(ReadOnlySpan<byte> line)
		{
			List<int> pipes = null;
			for (int i = 0; i < line.Length; i++)
				if (line[i] == AsciiPipe)
					(pipes = pipes ?? new List<int>()).Add(i);
			return pipes;
		}

		private R<T[]> Dersialize<T>(T[] arr, ReadOnlySpan<byte> line, List<int> pipes) where T : IMessage
		{
			if (pipes is null || pipes.Count == 0)
			{
				if (!ParseKeyValueLine(arr[0], line, null, null))
					return R.Err;
				return arr;
			}

			var arrItems = new HashSet<string>();
			var single = new List<string>();

			if (!ParseKeyValueLine(arr[arr.Length - 1], line.Slice(pipes[pipes.Count - 1] + 1).Trim(AsciiSpace), arrItems, null))
				return R.Err;

			for (int i = 0; i < pipes.Count - 1; i++)
			{
				if (!ParseKeyValueLine(arr[i + 1], line.Slice(pipes[i] + 1, pipes[i + 1] - pipes[i] - 1), arrItems, null))
					return R.Err;
			}

			// trim with the first one
			if (!ParseKeyValueLine(arr[0], line.Slice(0, pipes[0]), arrItems, single))
				return R.Err;

			if (arrItems.Count > 0)
			{
				arr[0].Expand((IMessage[])(object)arr, single);
			}
			return arr;
		}

		// data to response
		public R<T[]> GenerateResponse<T>(ReadOnlySpan<byte> line) where T : IResponse, new()
		{
			if (line.IsEmpty)
				return Array.Empty<T>();

			var pipes = PipeList(line);
			var arr = new T[(pipes?.Count ?? 0) + 1];
			for (int i = 0; i < arr.Length; i++)
				arr[i] = new T();
			return Dersialize(arr, line, pipes);
		}

		private bool ParseKeyValueLine(IMessage qm, ReadOnlySpan<byte> line, HashSet<string> indexing, List<string> single)
		{
			if (line.IsEmpty)
				return true;

			var ss = new SpanSplitter<byte>();
			ss.First(line, AsciiSpace);
			var key = ReadOnlySpan<byte>.Empty;
			var value = ReadOnlySpan<byte>.Empty;
			try
			{
				do
				{
					var param = ss.Trim(line);
					var kvpSplitIndex = param.IndexOf(AsciiEquals);
					key = kvpSplitIndex >= 0 ? param.Slice(0, kvpSplitIndex) : ReadOnlySpan<byte>.Empty;
					value = kvpSplitIndex <= param.Length - 1 ? param.Slice(kvpSplitIndex + 1) : ReadOnlySpan<byte>.Empty;

					if (!key.IsEmpty)
					{
						var keyStr = key.NewUtf8String();
						qm.SetField(keyStr, value, this);
						if (indexing != null)
						{
							if (single is null)
							{
								indexing.Add(keyStr);
							}
							else if (!indexing.Contains(keyStr))
							{
								single.Add(keyStr);
							}
							else
							{
								indexing = null;
								single = null;
							}
						}
					}

					if (!ss.HasNext)
						break;
					line = ss.Next(line);
				} while (line.Length > 0);
				return true;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Deserialization format error. Data: class:{0} field:{1} value:{2} msg:{3}", qm.GetType().Name, key.NewUtf8String(), value.NewUtf8String(), line.NewUtf8String());
				return false;
			}
		}
	}
}
