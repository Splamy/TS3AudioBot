// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Sessions
{
	using Helper;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using TS3Client;
	using TS3Client.Messages;
	using Response = System.Func<CommandSystem.ExecutionInformation, string>;

	public sealed class UserSession
	{
		private Dictionary<Type, object> assocMap;
		private bool lockToken;
		private readonly ClientData client;

		public Response ResponseProcessor { get; private set; }
		public object ResponseData { get; private set; }

		public Bot Bot { get; }

		public UserSession(Bot bot, ClientData client)
		{
			this.client = client;
			Bot = bot;
			ResponseProcessor = null;
			ResponseData = null;
		}

		public void Write(string message, TextMessageTargetMode targetMode)
		{
			VerifyLock();

			try
			{
				R result;
				switch (targetMode)
				{
				case TextMessageTargetMode.Private:
					result = Bot.QueryConnection.SendMessage(message, client.ClientId);
					break;
				case TextMessageTargetMode.Channel:
					result = Bot.QueryConnection.SendChannelMessage(message);
					break;
				case TextMessageTargetMode.Server:
					result = Bot.QueryConnection.SendServerMessage(message);
					break;
				default:
					throw new InvalidOperationException();
				}

				if (!result)
					Log.Write(Log.Level.Error, "Could not write message (Err:{0}) (Msg:{1})", result.Message, message);
			}
			catch (Ts3CommandException ex)
			{
				Log.Write(Log.Level.Error, "Could not write message (Ex:{0}) (Msg:{1})", ex.UnrollException(), message);
			}
		}

		public void SetResponse(Response responseProcessor, object responseData)
		{
			VerifyLock();

			ResponseProcessor = responseProcessor;
			ResponseData = responseData;
		}

		public void ClearResponse()
		{
			VerifyLock();

			ResponseProcessor = null;
			ResponseData = null;
		}

		public R<TData> Get<TAssoc, TData>()
		{
			VerifyLock();

			if (assocMap == null)
				return "Value not set";

			if (!assocMap.TryGetValue(typeof(TAssoc), out object value))
				return "Value not set";

			if (value?.GetType() != typeof(TData))
				return "Invalid request type";

			return (TData)value;
		}

		public void Set<TAssoc, TData>(TData data)
		{
			VerifyLock();

			if (assocMap == null)
				Util.Init(ref assocMap);

			if (assocMap.ContainsKey(typeof(TAssoc)))
				assocMap[typeof(TAssoc)] = data;
			else
				assocMap.Add(typeof(TAssoc), data);
		}

		public SessionToken GetLock()
		{
			var sessionToken = new SessionToken(this);
			sessionToken.Take();
			return sessionToken;
		}

		private void VerifyLock()
		{
			if (!lockToken)
				throw new InvalidOperationException("No access lock is currently active");
		}

		public sealed class SessionToken : IDisposable
		{
			private readonly UserSession session;
			public SessionToken(UserSession session) { this.session = session; }

			public void Take() { Monitor.Enter(session); session.lockToken = true; }
			public void Free() { Monitor.Exit(session); session.lockToken = false; }
			public void Dispose() => Free();
		}
	}
}
