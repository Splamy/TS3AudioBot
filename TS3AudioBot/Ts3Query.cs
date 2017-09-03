// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

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
			if (!tsBaseClient.Connected)
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

			tsQueryClient.RegisterNotification(TextMessageTargetMode.Server, 0);
			tsQueryClient.RegisterNotification(TextMessageTargetMode.Private, 0);
			tsQueryClient.RegisterNotification(ReasonIdentifier.Server, 0);

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
