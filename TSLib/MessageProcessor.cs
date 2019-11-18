// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TSLib.Helper;
using TSLib.Messages;

namespace TSLib
{
	internal abstract class BaseMessageProcessor
	{
		protected static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		protected readonly List<WaitBlock>[] dependingBlocks;
		private readonly Func<string, NotificationType> findTypeOfNotification;
		public Deserializer Deserializer { get; } = new Deserializer();

		protected ReadOnlyMemory<byte> cmdLineBuffer;
		protected readonly object waitBlockLock = new object();
		private const byte AsciiSpace = (byte)' ';

		protected BaseMessageProcessor(Func<string, NotificationType> findTypeOfNotification)
		{
			dependingBlocks = new List<WaitBlock>[Enum.GetValues(typeof(NotificationType)).Length];
			this.findTypeOfNotification = findTypeOfNotification;
		}

		public LazyNotification? PushMessage(ReadOnlyMemory<byte> message)
		{
			var msgSpan = message.Span;
			string notifyname;
			int splitindex = msgSpan.IndexOf(AsciiSpace);
			if (splitindex < 0)
				notifyname = msgSpan.TrimEnd(AsciiSpace).NewUtf8String();
			else
				notifyname = msgSpan.Slice(0, splitindex).NewUtf8String();

			bool hasEqual;
			NotificationType ntfyType;
			if ((hasEqual = notifyname.IndexOf('=') >= 0)
				|| (ntfyType = findTypeOfNotification(notifyname)) == NotificationType.Unknown)
			{
				if (!hasEqual)
					Log.Debug("Maybe unknown notification: {0}", notifyname);
				cmdLineBuffer = message;
				return null;
			}

			var lineDataPart = splitindex < 0 ? ReadOnlySpan<byte>.Empty : msgSpan.Slice(splitindex);

			// if it's not an error it is a notification
			if (ntfyType != NotificationType.CommandError)
			{
				var notification = Deserializer.GenerateNotification(lineDataPart, ntfyType);
				if (!notification.Ok)
				{
					Log.Warn("Got unparsable message. ({0})", msgSpan.NewUtf8String());
					return null;
				}

				var lazyNotification = new LazyNotification(notification.Value, ntfyType);
				lock (waitBlockLock)
				{
					var dependantList = dependingBlocks[(int)ntfyType];
					if (dependantList != null)
					{
						foreach (var item in dependantList)
						{
							item.SetNotification(lazyNotification);
							if (item.DependsOn != null)
							{
								foreach (var otherDepType in item.DependsOn)
								{
									if (otherDepType == ntfyType)
										continue;
									dependingBlocks[(int)otherDepType]?.Remove(item);
								}
							}
						}
						dependantList.Clear();
					}
				}

				return lazyNotification;
			}

			var result = Deserializer.GenerateSingleNotification(lineDataPart, NotificationType.CommandError);
			var errorStatus = result.Ok ? (CommandError)result.Value : CommandError.Custom("Invalid Error code");

			return PushMessageInternal(errorStatus, ntfyType);
		}

		protected abstract LazyNotification? PushMessageInternal(CommandError errorStatus, NotificationType ntfyType);

		public abstract void DropQueue();
	}

	internal sealed class AsyncMessageProcessor : BaseMessageProcessor
	{
		private readonly ConcurrentDictionary<string, WaitBlock> requestDict;

		public AsyncMessageProcessor(Func<string, NotificationType> findTypeOfNotification) : base(findTypeOfNotification)
		{
			requestDict = new ConcurrentDictionary<string, WaitBlock>();
		}

		protected override LazyNotification? PushMessageInternal(CommandError errorStatus, NotificationType ntfyType)
		{
			if (string.IsNullOrEmpty(errorStatus.ReturnCode))
			{
				return new LazyNotification(new[] { errorStatus }, ntfyType);
			}

			// otherwise it is the result status code to a request
			lock (waitBlockLock)
			{
				if (requestDict.TryRemove(errorStatus.ReturnCode, out var waitBlock))
				{
					waitBlock.SetAnswer(errorStatus, cmdLineBuffer);
					cmdLineBuffer = null;
				}
				else { /* ??? */ }
			}

			return null;
		}

		public void EnqueueRequest(string returnCode, WaitBlock waitBlock)
		{
			lock (waitBlockLock)
			{
				if (!requestDict.TryAdd(returnCode, waitBlock))
					throw new InvalidOperationException("Trying to add already existing WaitBlock returnCode");
				if (waitBlock.DependsOn != null)
				{
					foreach (var dependantType in waitBlock.DependsOn)
					{
						var depentantList = dependingBlocks[(int)dependantType];
						if (depentantList is null)
							dependingBlocks[(int)dependantType] = depentantList = new List<WaitBlock>();

						depentantList.Add(waitBlock);
					}
				}
			}
		}

		public override void DropQueue()
		{
			lock (waitBlockLock)
			{
				foreach (var wb in requestDict.Values)
					wb.SetAnswer(CommandError.TimeOut);
				requestDict.Clear();

				foreach (var block in dependingBlocks)
				{
					block?.ForEach((Action<WaitBlock>)(wb => wb.SetAnswer(CommandError.TimeOut)));
					block?.Clear();
				}
			}
		}
	}

	internal sealed class SyncMessageProcessor : BaseMessageProcessor
	{
		private readonly ConcurrentQueue<WaitBlock> requestQueue;

		public SyncMessageProcessor(Func<string, NotificationType> findTypeOfNotification) : base(findTypeOfNotification)
		{
			requestQueue = new ConcurrentQueue<WaitBlock>();
		}

		protected override LazyNotification? PushMessageInternal(CommandError errorStatus, NotificationType ntfyType)
		{
			if (!requestQueue.IsEmpty && requestQueue.TryDequeue(out var waitBlock))
			{
				waitBlock.SetAnswer(errorStatus, cmdLineBuffer);
				cmdLineBuffer = null;
			}
			else { /* ??? */ }

			return null;
		}

		public void EnqueueRequest(WaitBlock waitBlock)
		{
			requestQueue.Enqueue(waitBlock);
		}

		public override void DropQueue()
		{
			while (!requestQueue.IsEmpty && requestQueue.TryDequeue(out var waitBlock))
				waitBlock.SetAnswer(CommandError.TimeOut);
		}
	}
}
