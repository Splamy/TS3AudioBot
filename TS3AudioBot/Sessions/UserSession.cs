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

namespace TS3AudioBot.Sessions
{
	using Helper;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using TS3Client;
	using TS3Client.Messages;
	using Response = System.Func<CommandSystem.ExecutionInformation, string>;

	public sealed class UserSession
	{
		private const string TokenFormat = "{0}:" + Web.WebManager.WebRealm + ":{1}";

		private Dictionary<Type, object> assocMap = null;
		private bool lockToken = false;

		public Response ResponseProcessor { get; private set; }
		public object ResponseData { get; private set; }

		public MainBot Bot { get; }
		public ushort ActiveClientId { get; set; }
		public ClientData Client { get; private set; }

		internal ApiToken Token { get; set; }
		internal bool HasActiveToken => Token != null && Token.ApiTokenActive;

		public UserSession(MainBot bot, ClientData client)
		{
			Bot = bot;
			Client = client;
			ResponseProcessor = null;
			ResponseData = null;
			Token = null;
		}

		public void Write(string message, bool isPrivate)
		{
			VerifyLock();

			try
			{
				R result;
				if (isPrivate)
					result = Bot.QueryConnection.SendMessage(message, Client.ClientId);
				else
					result = Bot.QueryConnection.SendGlobalMessage(message);

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

			object value;
			if (!assocMap.TryGetValue(typeof(TAssoc), out value))
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

		public R UpdateClient(ushort newId)
		{
			var result = Bot.QueryConnection.GetClientById(newId);
			if (result.Ok)
			{
				if (result.Value.Uid != Client.Uid)
					return "Uid does not match";
				Client = result.Value;
				return R.OkR;
			}
			return result.Message;
		}

		public bool HasAdminRights()
		{
			Log.Write(Log.Level.Debug, "AdminCheck called!");
			var clientSgIds = Bot.QueryConnection.GetClientServerGroups(Client.DatabaseId);
			return clientSgIds.Contains(Bot.mainBotData.AdminGroupId);
		}

		public R<string> GenerateToken() => GenerateToken(ApiToken.DefaultTokenTimeout);
		public R<string> GenerateToken(TimeSpan timeout)
		{
			if (Token == null)
				Token = new ApiToken();

			Token.Value = TextUtil.GenToken(ApiToken.TokenLen);
			var newTimeout = Util.GetNow() + timeout;
			if (newTimeout > Token.Timeout)
				Token.Timeout = newTimeout;

			return R<string>.OkR(string.Format(TokenFormat, Client.Uid, Token.Value));
		}

		public sealed class SessionToken : IDisposable
		{
			private UserSession session;
			public SessionToken(UserSession session) { this.session = session; }

			public void Take() { Monitor.Enter(session); session.lockToken = true; }
			public void Free() { Monitor.Exit(session); session.lockToken = false; }
			public void Dispose() => Free();
		}
	}
}
