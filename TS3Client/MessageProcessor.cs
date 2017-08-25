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
	using Commands;
	using Messages;
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;

	internal class MessageProcessor
	{
		private readonly ConcurrentDictionary<string, WaitBlock> requestDict;
		private readonly ConcurrentQueue<WaitBlock> requestQueue;
		private readonly bool synchronQueue;
		private readonly object dependantBlockLock = new object();
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
				var dependantList = dependingBlocks[(int)ntfyType];
				if (dependantList != null)
				{
					lock (dependantBlockLock)
					{
						foreach (var item in dependantList)
						{
							if (!item.Closed)
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
				WaitBlock waitBlock;
				if (!requestQueue.IsEmpty && requestQueue.TryDequeue(out waitBlock))
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
				WaitBlock waitBlock;
				if (requestDict.TryRemove(errorStatus.ReturnCode, out waitBlock))
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
			if (synchronQueue)
				throw new InvalidOperationException();
			if (!requestDict.TryAdd(returnCode, waitBlock))
				throw new InvalidOperationException("Trying to add alreading existing WaitBlock returnCode");
			if (waitBlock.DependsOn != null)
			{
				lock (dependantBlockLock)
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
				WaitBlock waitBlock;
				while (!requestQueue.IsEmpty && requestQueue.TryDequeue(out waitBlock))
					waitBlock.SetAnswer(
						new CommandError { Id = Ts3ErrorCode.custom_error, Message = "Connection Closed" });
			}
			else
			{
				var arr = requestDict.ToArray();
				requestDict.Clear();
				foreach (var block in dependingBlocks)
					block?.Clear();
				foreach (var val in arr)
					val.Value.SetAnswer(
						new CommandError { Id = Ts3ErrorCode.custom_error, Message = "Connection Closed" });
			}
		}
	}

	/*internal class AsyncMessageProcessor : MessageProcessor
	{

	}*/
}
