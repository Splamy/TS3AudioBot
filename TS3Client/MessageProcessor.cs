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
	using Messages;
	using Commands;
	using System;
	using System.Linq;
	using System.Collections.Generic;

	internal class MessageProcessor
	{
		private readonly Dictionary<string, WaitBlock> requestDict;
		private readonly Queue<WaitBlock> requestQueue;
		private readonly bool synchronQueue;

		private string cmdLineBuffer;

		public MessageProcessor(bool synchronQueue)
		{
			this.synchronQueue = synchronQueue;
			if (synchronQueue)
				requestQueue = new Queue<WaitBlock>();
			else
				requestDict = new Dictionary<string, WaitBlock>();
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
				return lazyNotification;
			}

			var errorStatus = (CommandError)CommandDeserializer.GenerateSingleNotification(lineDataPart, NotificationType.Error);

			if (synchronQueue)
			{
				if (requestQueue.Any())
				{
					var waitBlock = requestQueue.Dequeue();
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
				if (requestDict.TryGetValue(errorStatus.ReturnCode, out waitBlock))
				{
					requestDict.Remove(errorStatus.ReturnCode);
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
			requestDict.Add(returnCode, waitBlock);
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
				while (requestQueue.Any())
					requestQueue.Dequeue().SetAnswer(
						new CommandError { Id = Ts3ErrorCode.custom_error, Message = "Connection Closed" });
			}
			else
			{
				var arr = requestDict.ToArray();
				requestDict.Clear();
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
