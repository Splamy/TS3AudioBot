using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TeamSpeak3QueryApi.Net.Specialized;
using TeamSpeak3QueryApi.Net.Specialized.Responses;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;

using Response = System.Func<TS3AudioBot.BotSession, TeamSpeak3QueryApi.Net.Specialized.Notifications.TextMessage, bool, bool>;

namespace TS3AudioBot
{
	abstract class BotSession
	{
		protected QueryConnection queryConnection;

		public AudioRessource userRessource { get; set; }
		public Response responseProcessor { get; protected set; }
		public bool adminResponse { get; protected set; }
		public object responseData { get; protected set; }

		public abstract bool IsPrivate { get; }

		public abstract void Write(string message);

		public BotSession(QueryConnection queryConnection)
		{
			this.queryConnection = queryConnection;
			userRessource = null;
			responseProcessor = null;
			responseData = null;
		}

		public void SetResponse(Response responseProcessor, object responseData, bool requiresAdminCheck)
		{
			this.responseProcessor = responseProcessor;
			this.responseData = responseData;
			this.adminResponse = requiresAdminCheck;
		}

		public void ClearResponse()
		{
			responseProcessor = null;
			responseData = null;
			adminResponse = false;
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
