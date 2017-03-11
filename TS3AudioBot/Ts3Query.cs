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
	using Helper;
	using System;
	using TS3Client;
	using TS3Client.Messages;
	using TS3Client.Query;

	public class Ts3Query : TeamspeakControl
	{
		private readonly Ts3QueryClient tsQueryClient;
		private QueryConnectionData connectionData;
		private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(60);

		public Ts3Query(QueryConnectionData qcd) : base(ClientType.Query)
		{
			tsQueryClient = (Ts3QueryClient)tsBaseClient;
			connectionData = qcd;
		}

		public override void Connect()
		{
			if (!tsBaseClient.IsConnected)
			{
				tsQueryClient.Connect(new ConnectionData() { Hostname = connectionData.host, Port = connectionData.port });
				tsQueryClient.Login(connectionData.user, connectionData.passwd);
				tsQueryClient.UseServer(1);
				try { tsQueryClient.ChangeName("TS3AudioBot"); }
				catch (Ts3CommandException) { Log.Write(Log.Level.Warning, "TS3AudioBot name already in use!"); }
			}
		}

		protected override void OnConnected(object sender, EventArgs e)
		{
			base.OnConnected(sender, e);

			tsQueryClient.RegisterNotification(MessageTarget.Server, -1);
			tsQueryClient.RegisterNotification(MessageTarget.Private, -1);
			tsQueryClient.RegisterNotification(RequestTarget.Server, -1);

			TickPool.RegisterTick(() => tsBaseClient.WhoAmI(), PingInterval, true);
		}

		protected override ClientData GetSelf()
		{
			var data = tsBaseClient.WhoAmI();
			var cd = new ClientData
			{
				ChannelId = data.ChannelId,
				DatabaseId = data.DatabaseId,
				ClientId = data.ClientId,
				NickName = data.NickName,
				ClientType = tsBaseClient.ClientType
			};
			return cd;
		}
	}

	public struct QueryConnectionData
	{
		[Info("the address of the TeamSpeak3 Query")]
		public string host;
		[Info("the port of the TeamSpeak3 Query", "10011")]
		public ushort port;
		[Info("the user for the TeamSpeak3 Query")]
		public string user;
		[Info("the password for the TeamSpeak3 Query")]
		public string passwd;
	}
}
