namespace TS3AudioBot
{
	using System;
	using TS3Query;
	using TS3Query.Messages;
	using Response = System.Func<CommandSystem.ExecutionInformation, bool>;

	public abstract class BotSession : MarshalByRefObject
	{
		public MainBot Bot { get; private set; }

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

	internal sealed class PrivateSession : BotSession
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
