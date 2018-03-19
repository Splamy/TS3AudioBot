// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client
{
	using Helper;
	using Messages;
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;

	internal abstract class BaseMessageProcessor
	{
		protected static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		protected readonly List<WaitBlock>[] dependingBlocks;

		protected string cmdLineBuffer;
		protected readonly object waitBlockLock = new object();

		public BaseMessageProcessor()
		{
			dependingBlocks = new List<WaitBlock>[Enum.GetValues(typeof(NotificationType)).Length];
		}

		public LazyNotification? PushMessage(string message)
		{
			string notifyname;
			int splitindex = message.IndexOf(' ');
			if (splitindex < 0)
				notifyname = message.TrimEnd();
			else
				notifyname = message.Substring(0, splitindex);

			bool hasEqual;
			NotificationType ntfyType;
			if ((hasEqual = notifyname.IndexOf('=') >= 0)
				|| (ntfyType = MessageHelper.GetNotificationType(notifyname)) == NotificationType.Unknown)
			{
				if (!hasEqual)
					Log.Debug("Maybe unknown notification: {0}", notifyname);
				cmdLineBuffer = message;
				return null;
			}

			var lineDataPart = splitindex < 0 ? "" : message.Substring(splitindex);

			// if it's not an error it is a notification
			if (ntfyType != NotificationType.CommandError)
			{
				var notification = Deserializer.GenerateNotification(lineDataPart, ntfyType);
				var lazyNotification = new LazyNotification(notification, ntfyType);
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
			var errorStatus = result.Ok ? (CommandError)result.Value : Util.CustomError("Invalid Error code");

			return PushMessageInternal(errorStatus, ntfyType);
		}

		protected abstract LazyNotification? PushMessageInternal(CommandError errorStatus, NotificationType ntfyType);

		public abstract void DropQueue();
	}

	internal sealed class AsyncMessageProcessor : BaseMessageProcessor
	{
		private readonly ConcurrentDictionary<string, WaitBlock> requestDict;

		public AsyncMessageProcessor()
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
						if (depentantList == null)
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
					wb.SetAnswer(Util.TimeOutCommandError);
				requestDict.Clear();

				foreach (var block in dependingBlocks)
				{
					block?.ForEach(wb => wb.SetAnswer(Util.TimeOutCommandError));
					block?.Clear();
				}
			}
		}
	}

	internal sealed class SyncMessageProcessor : BaseMessageProcessor
	{
		private readonly ConcurrentQueue<WaitBlock> requestQueue;

		public SyncMessageProcessor()
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
				waitBlock.SetAnswer(Util.TimeOutCommandError);
		}
	}
}
