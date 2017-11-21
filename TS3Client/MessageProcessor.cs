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
	using Commands;
	using Messages;
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;

	internal sealed class MessageProcessor
	{
		private readonly ConcurrentDictionary<string, WaitBlock> requestDict;
		private readonly ConcurrentQueue<WaitBlock> requestQueue;
		private readonly bool synchronQueue;
		private readonly object waitBlockLock = new object();
		private readonly List<WaitBlock>[] dependingBlocks;

		private string cmdLineBuffer;

		public MessageProcessor(bool synchronQueue)
		{
			this.synchronQueue = synchronQueue;
			if (synchronQueue)
			{
				requestQueue = new ConcurrentQueue<WaitBlock>();
			}
			else
			{
				requestDict = new ConcurrentDictionary<string, WaitBlock>();
				dependingBlocks = new List<WaitBlock>[Enum.GetValues(typeof(NotificationType)).Length];
			}
		}

		public LazyNotification? PushMessage(string message)
		{
			string notifyname;
			int splitindex = message.IndexOf(' ');
			if (splitindex < 0)
				notifyname = message.TrimEnd();
			else
				notifyname = message.Substring(0, splitindex);

			var ntfyType = MessageHelper.GetNotificationType(notifyname);
			if (ntfyType == NotificationType.Unknown)
			{
				cmdLineBuffer = message;
				return null;
			}

			var lineDataPart = splitindex < 0 ? "" : message.Substring(splitindex);

			// if it's not an error it is a notification
			if (ntfyType != NotificationType.Error)
			{
				var notification = CommandDeserializer.GenerateNotification(lineDataPart, ntfyType);
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

			var errorStatus = (CommandError)CommandDeserializer.GenerateSingleNotification(lineDataPart, NotificationType.Error);

			if (synchronQueue)
			{
				if (!requestQueue.IsEmpty && requestQueue.TryDequeue(out var waitBlock))
				{
					waitBlock.SetAnswer(errorStatus, cmdLineBuffer);
					cmdLineBuffer = null;
				}
				else { /* ??? */ }
			}
			else
			{
				// now check if this error is an answer to a request we made
				// if there is no return code provided it means it is a error-notification
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
			}

			return null;
		}

		public void EnqueueRequest(string returnCode, WaitBlock waitBlock)
		{
			if (synchronQueue)
				throw new InvalidOperationException();

			lock (waitBlockLock)
			{
				if (!requestDict.TryAdd(returnCode, waitBlock))
					throw new InvalidOperationException("Trying to add alreading existing WaitBlock returnCode");
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

		public void EnqueueRequest(WaitBlock waitBlock)
		{
			if (!synchronQueue)
				throw new InvalidOperationException();
			requestQueue.Enqueue(waitBlock);
		}

		public void DropQueue()
		{
			if (synchronQueue)
			{
				while (!requestQueue.IsEmpty && requestQueue.TryDequeue(out WaitBlock waitBlock))
					waitBlock.SetAnswer(Util.TimeOutCommandError);
			}
			else
			{
				lock (waitBlockLock)
				{
					foreach (var wb in requestDict.Values)
						wb.SetAnswer(Util.TimeOutCommandError);
					requestDict.Clear();

					foreach (var block in dependingBlocks)
					{
						block.ForEach(wb => wb.SetAnswer(Util.TimeOutCommandError));
						block?.Clear();
					}
				}
			}
		}
	}
}
