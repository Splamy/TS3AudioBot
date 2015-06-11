using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TeamSpeak3QueryApi.Net.Specialized;
using TeamSpeak3QueryApi.Net.Specialized.Responses;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;

namespace TS3AudioBot
{
	abstract class BotSession
	{
		protected QueryConnection queryConnection;

		public AudioRessource userRessource { get; set; }
		public Func<BotSession, TextMessage, bool> responseProcessor { get; set; }
		public object responseData { get; set; }

		public abstract bool IsPrivate { get; }

		public abstract void Write(string message);

		public BotSession(QueryConnection queryConnection)
		{
			this.queryConnection = queryConnection;
			userRessource = null;
			responseProcessor = null;
			responseData = null;
		}
	}

	sealed class PublicSession : BotSession
	{
		public override bool IsPrivate { get { return false; } }

		public override async void Write(string message)
		{
			try
			{
				await queryConnection.TSClient.SendGlobalMessage(message);
			}
			catch (Exception ex)
			{
				Log.Write(Log.Level.Error, "Could not write public message ({0})", ex);
			}
		}

		public PublicSession(QueryConnection queryConnection)
			: base(queryConnection)
		{ }
	}

	sealed class PrivateSession : BotSession
	{
		public GetClientsInfo client { get; private set; }

		public override bool IsPrivate { get { return true; } }

		public override async void Write(string message)
		{
			await queryConnection.TSClient.SendMessage(message, client);
		}

		public PrivateSession(QueryConnection queryConnection, GetClientsInfo client)
			: base(queryConnection)
		{
			this.client = client;
		}
	}
}
