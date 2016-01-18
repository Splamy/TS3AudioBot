namespace TS3AudioBot
{
	using TS3Query;
	using TS3Query.Messages;
	using Response = System.Func<BotSession, TS3Query.Messages.TextMessage, bool, bool>;

	abstract class BotSession
	{
		public MainBot Bot { get; private set; }

		public PlayData UserResource { get; set; }
		public Response ResponseProcessor { get; protected set; }
		public bool AdminResponse { get; protected set; }
		public object ResponseData { get; protected set; }

		public abstract bool IsPrivate { get; }

		public abstract void Write(string message);

		protected BotSession(MainBot bot)
		{
			Bot = bot;
			UserResource = null;
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
			catch (QueryCommandException ex)
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
		public ClientData Client { get; private set; }

		public override bool IsPrivate { get { return true; } }

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
