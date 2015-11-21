using System;
using TeamSpeak3QueryApi.Net;

namespace TS3AudioBot
{
	using TeamSpeak3QueryApi.Net.Specialized.Responses;
	using Response = Func<BotSession, TeamSpeak3QueryApi.Net.Specialized.Notifications.TextMessage, bool, bool>;

	abstract class BotSession
	{
		public MainBot Bot { get; private set; }

		public PlayData UserRessource { get; set; }
		public Response ResponseProcessor { get; protected set; }
		public bool AdminResponse { get; protected set; }
		public object ResponseData { get; protected set; }

		public abstract bool IsPrivate { get; }

		public abstract void Write(string message);

		protected BotSession(MainBot bot)
		{
			Bot = bot;
			UserRessource = null;
			ResponseProcessor = null;
			ResponseData = null;
		}

		public void SetResponse(Response responseProcessor, object responseData, bool requiresAdminCheck)
		{
			ResponseProcessor = responseProcessor;
			ResponseData = responseData;
			AdminResponse = requiresAdminCheck;
		}

		public void ClearResponse()
		{
			ResponseProcessor = null;
			ResponseData = null;
			AdminResponse = false;
		}
	}

	sealed class PublicSession : BotSession
	{
		public override bool IsPrivate { get { return false; } }

		public override void Write(string message)
		{
			try
			{
				Bot.QueryConnection.SendGlobalMessage(message);
			}
			catch (QueryException ex)
			{
				Log.Write(Log.Level.Error, "Could not write public message ({0})", ex);
			}
		}

		public PublicSession(MainBot bot)
			: base(bot)
		{ }
	}

	sealed class PrivateSession : BotSession
	{
		public GetClientsInfo Client { get; private set; }

		public override bool IsPrivate { get { return true; } }

		public override void Write(string message)
		{
			Bot.QueryConnection.SendMessage(message, Client);
		}

		public PrivateSession(MainBot bot, GetClientsInfo client)
			: base(bot)
		{
			Client = client;
		}
	}
}
