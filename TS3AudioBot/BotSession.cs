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
		public abstract bool HasAdminRights { get; }

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
		public override bool HasAdminRights { get { return false; } }

		public override async void Write(string message)
		{
			await queryConnection.TSClient.SendGlobalMessage(message);
		}

		public PublicSession(QueryConnection queryConnection)
			: base(queryConnection)
		{ }
	}

	sealed class PrivateSession : BotSession
	{
		public GetClientsInfo client { get; private set; }
		private bool hasAdminRights;

		public override bool IsPrivate { get { return true; } }
		public override bool HasAdminRights { get { return hasAdminRights; } }

		public override async void Write(string message)
		{
			await queryConnection.TSClient.SendMessage(message, client);
		}

		public PrivateSession(QueryConnection queryConnection, GetClientsInfo client, bool hasAdminRights)
			: base(queryConnection)
		{
			this.client = client;
			this.hasAdminRights = hasAdminRights;
		}
	}
}
