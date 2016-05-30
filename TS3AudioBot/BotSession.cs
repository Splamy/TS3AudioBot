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
	using TS3Query;
	using TS3Query.Messages;
	using Response = System.Func<CommandSystem.ExecutionInformation, bool>;

	public abstract class BotSession : MarshalByRefObject
	{
		public MainBot Bot { get; }

		public Response ResponseProcessor { get; protected set; }
		public object ResponseData { get; protected set; }

		public abstract bool IsPrivate { get; }

		public abstract void Write(string message);

		protected BotSession(MainBot bot)
		{
			Bot = bot;
			ResponseProcessor = null;
			ResponseData = null;
		}

		public void SetResponse(Response responseProcessor, object responseData)
		{
			ResponseProcessor = responseProcessor;
			ResponseData = responseData;
		}

		public void ClearResponse()
		{
			ResponseProcessor = null;
			ResponseData = null;
		}
	}

	internal sealed class PublicSession : BotSession
	{
		public override bool IsPrivate => false;

		public override void Write(string message)
		{
			try
			{
				Bot.QueryConnection.SendGlobalMessage(message);
			}
			catch (QueryCommandException ex)
			{
				Log.Write(Log.Level.Error, "Could not write public message ({0})", ex);
			}
		}

		public PublicSession(MainBot bot)
			: base(bot)
		{ }
	}

	internal sealed class PrivateSession : BotSession
	{
		public ClientData Client { get; }

		public override bool IsPrivate => true;

		public override void Write(string message)
		{
			Bot.QueryConnection.SendMessage(message, Client);
		}

		public PrivateSession(MainBot bot, ClientData client)
			: base(bot)
		{
			Client = client;
		}
	}
}
