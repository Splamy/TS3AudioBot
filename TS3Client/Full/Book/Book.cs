// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Full.Book
{
	using Messages;

#pragma warning disable CS8019
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	using i8 = System.SByte;
	using u8 = System.Byte;
	using i16 = System.Int16;
	using u16 = System.UInt16;
	using i32 = System.Int32;
	using u32 = System.UInt32;
	using i64 = System.Int64;
	using u64 = System.UInt64;
	using f32 = System.Single;
	using d64 = System.Double;
	using str = System.String;

	using Duration = System.TimeSpan;
	using DurationSeconds = System.TimeSpan;
	using DurationMilliseconds = System.TimeSpan;
	using SocketAddr = System.Net.IPAddress;

	using Uid = System.String;
	using ClientDbId = System.UInt64;
	using ClientId = System.UInt16;
	using ChannelId = System.UInt64;
	using ServerGroupId = System.UInt64;
	using ChannelGroupId = System.UInt64;
	using IconHash = System.Int32;
	using ConnectionId = System.UInt32;
#pragma warning restore CS8019

	public partial class Connection
	{
		// TODO
		// Many operations can be checked if they were successful (like remove or get).
		// In cases which this fails we should print an error.

		private void SetServer(Server server)
		{
			Server = server;
		}

		private Channel GetChannel(ChannelId id)
		{
			if (Server.Channels.TryGetValue(id, out var channel))
				return channel;
			return null;
		}

		private void SetChannel(Channel channel, ChannelId id)
		{
			Server.Channels[id] = channel;
		}

		private void RemoveChannel(ChannelId id)
		{
			Server.Channels.Remove(id);
		}

		private Client GetClient(ClientId id)
		{
			if (Server.Clients.TryGetValue(id, out var client))
				return client;
			return null;
		}

		private void SetClient(Client client, ClientId id)
		{
			Server.Clients[id] = client;
		}

		private void RemoveClient(ClientId id)
		{
			Server.Clients.Remove(id);
		}

		private void SetConnectionClientData(ConnectionClientData connectionClientData, ClientId id)
		{
			if (!Server.Clients.TryGetValue(id, out var client))
				return;
			client.ConnectionData = connectionClientData;
		}

		private void SetServerGroup(ServerGroup serverGroup, ServerGroupId id)
		{
			Server.Groups[id] = serverGroup;
		}

		private Server GetServer()
		{
			return Server;
		}

		// Manual move functions

		private (u16, MaxFamilyClients) MaxClientsCcFun(ChannelCreated msg) => MaxClientsFun(msg.MaxClients, msg.IsMaxClientsUnlimited, msg.MaxFamilyClients, msg.IsMaxFamilyClientsUnlimited, msg.InheritsMaxFamilyClients);
		private (u16, MaxFamilyClients) MaxClientsCeFun(ChannelEdited msg) => MaxClientsFun(msg.MaxClients, msg.IsMaxClientsUnlimited, msg.MaxFamilyClients, msg.IsMaxFamilyClientsUnlimited, msg.InheritsMaxFamilyClients);
		private (u16, MaxFamilyClients) MaxClientsClFun(ChannelList msg) => MaxClientsFun(msg.MaxClients, msg.IsMaxClientsUnlimited, msg.MaxFamilyClients, msg.IsMaxFamilyClientsUnlimited, msg.InheritsMaxFamilyClients);
		private (u16, MaxFamilyClients) MaxClientsFun(i32 MaxClients, bool IsMaxClientsUnlimited, i32 MaxFamilyClients, bool IsMaxFamilyClientsUnlimited, bool InheritsMaxFamilyClients)
		{
			u16 maxClient;
			if (IsMaxClientsUnlimited)
				maxClient = u16.MaxValue; // TODO to optional
			else
				maxClient = (u16)Math.Max(Math.Min(ushort.MaxValue, MaxClients), 0);
			var fam = new MaxFamilyClients();
			if (IsMaxFamilyClientsUnlimited) fam.LimitKind = MaxFamilyClientsKind.Unlimited;
			else if (InheritsMaxFamilyClients) fam.LimitKind = MaxFamilyClientsKind.Inherited;
			else
			{
				fam.LimitKind = MaxFamilyClientsKind.Limited;
				fam.MaxFamiliyClients = (u16)Math.Max(Math.Min(ushort.MaxValue, MaxFamilyClients), 0);
			}
			return (maxClient, fam);
		}

		private ChannelType ChannelTypeCcFun(ChannelCreated msg) => default; // TODO
		private ChannelType ChannelTypeCeFun(ChannelEdited msg) => default; // TODO
		private ChannelType ChannelTypeClFun(ChannelList msg) => default; // TODO

		private str AwayFun(ClientEnterView msg) => default; // TODO
		private TalkPowerRequest TalkPowerFun(ClientEnterView msg) => default; // TODO
		private str[] BadgesFun(ClientEnterView msg) => Array.Empty<string>(); // TODO

		private SocketAddr AddressFun(ClientConnectionInfo msg) => SocketAddr.Any; // TODO

		private void SetClientDataFun(InitServer initServer)
		{
			OwnClient = initServer.ClientId;
		}

		private static bool ReturnFalse<T>(T _) => false;
		private static object ReturnNone<T>(T _) => null;
	}
}
