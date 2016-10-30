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

namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using Helper;
	using TS3Client;
	using TS3Client.Messages;
	using Response = System.Func<CommandSystem.ExecutionInformation, bool>;

	public sealed class UserSession
	{
		private Dictionary<Type, object> assocMap = null;
		private bool tokenToken = false;

		public Response ResponseProcessor { get; private set; }
		public object ResponseData { get; private set; }

		public MainBot Bot { get; }
		public ClientData ClientCached { get; private set; }
		public ClientData Client => ClientCached = Bot.QueryConnection.GetClientById(ClientCached.ClientId);
		public bool IsPrivate { get; internal set; }

		public UserSession(MainBot bot, ClientData client)
		{
			Bot = bot;
			ClientCached = client;
			ResponseProcessor = null;
			ResponseData = null;
		}

		public void Write(string message)
		{
			if (!tokenToken)
				throw new InvalidOperationException("No token is currently active");

			try
			{
				if (IsPrivate)
					Bot.QueryConnection.SendMessage(message, ClientCached.ClientId);
				else
					Bot.QueryConnection.SendGlobalMessage(message);
			}
			catch (Ts3CommandException ex)
			{
				Log.Write(Log.Level.Error, "Could not write public message ({0})", ex);
			}
		}

		public void SetResponse(Response responseProcessor, object responseData)
		{
			if (!tokenToken)
				throw new InvalidOperationException("No token is currently active");

			ResponseProcessor = responseProcessor;
			ResponseData = responseData;
		}

		public void ClearResponse()
		{
			if (!tokenToken)
				throw new InvalidOperationException("No token is currently active");

			ResponseProcessor = null;
			ResponseData = null;
		}

		public R<TData> Get<TAssoc, TData>()
		{
			if (!tokenToken)
				throw new InvalidOperationException("No token is currently active");

			if (assocMap == null)
				return "Value not set";

			object value;
			if (!assocMap.TryGetValue(typeof(TAssoc), out value))
				return "Value not set";

			if (value?.GetType() != typeof(TData))
				return "Invalid request type";

			return (TData)value;
		}

		public void Set<TAssoc, TData>(TData data)
		{
			if (!tokenToken)
				throw new InvalidOperationException("No token is currently active");

			if (assocMap == null)
				Util.Init(ref assocMap);

			if (assocMap.ContainsKey(typeof(TAssoc)))
				assocMap[typeof(TAssoc)] = data;
			else
				assocMap.Add(typeof(TAssoc), data);
		}

		public SessionToken GetToken(bool isPrivate)
		{
			var sessionToken = new SessionToken(this);
			sessionToken.Take();
			IsPrivate = isPrivate;
			return sessionToken;
		}

		public sealed class SessionToken : IDisposable
		{
			private UserSession session;
			public SessionToken(UserSession session) { this.session = session; }

			public void Take() { Monitor.Enter(session); session.tokenToken = true; }
			public void Free() { Monitor.Exit(session); session.tokenToken = false; }
			public void Dispose() => Free();
		}
	}
}
