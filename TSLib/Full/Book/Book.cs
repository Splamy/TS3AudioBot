// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using TSLib.Helper;
using TSLib.Messages;
using SocketAddr = System.String;

namespace TSLib.Full.Book
{
	public partial class Connection
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public Client? OwnClient { get; set; }
		public Channel? OwnChannel { get; set; }

		// Server
		private Server GetServer() => Server;
		private void SetServer(Server server) => Server = server;

		// OptionalServerData
		private OptionalServerData GetOptionalServerData() => Server.OptionalData ??= new();
		private void RemoveOptionalServerData() => Server.OptionalData = null;
		private ConnectionServerData GetConnectionServerData() => Server.ConnectionData ??= new();

		// Channel
		private Channel? GetChannel(ChannelId id)
			=> Channels.TryGetValue(id, out var channel) ? channel : null;
		private void SetChannel(Channel channel, ChannelId id)
		{
			channel.Id = id;
			Channels[id] = channel;
		}
		private void RemoveChannel(ChannelId id)
		{
			var cur = Channels[id];
			Channels.Remove(id);
			ChannelOrderRemove(id, cur.Order);
		}

		// OptionalChannelData
		private OptionalChannelData? GetOptionalChannelData(ChannelId id)
		{
			if (Channels.TryGetValue(id, out var channel))
				return channel.OptionalData ??= new();
			return null;
		}
		private void RemoveOptionalChannelData(ChannelId id)
		{
			if (Channels.TryGetValue(id, out var channel))
				channel.OptionalData = null;
		}

		// Client
		private Client? GetClient(ClientId id)
			=> Clients.TryGetValue(id, out var client) ? client : null;
		private void SetClient(Client client, ClientId id)
		{
			client.Id = id;
			Clients[id] = client;
		}
		private void RemoveClient(ClientId id) => Clients.Remove(id);

		// OptionalClientData
		private OptionalClientData? GetOptionalClientData(ClientId id)
		{
			if (Clients.TryGetValue(id, out var client))
				return client.OptionalData ??= new();
			return null;
		}

		// ConnectionClientData
		private ConnectionClientData? GetConnectionClientData(ClientId id)
		{
			if (Clients.TryGetValue(id, out var client))
				return client.ConnectionData ??= new();
			return null;
		}
		private void SetConnectionClientData(ConnectionClientData connectionClientData, ClientId id)
		{
			if (!Clients.TryGetValue(id, out var client))
				return;
			client.ConnectionData = connectionClientData;
		}

		// ServerGroup
		private void SetServerGroup(ServerGroup serverGroup, ServerGroupId id) => ServerGroups[id] = serverGroup;

		// ChannelGroup
		private void SetChannelGroup(ChannelGroup channelGroup, ChannelGroupId id) => ChannelGroups[id] = channelGroup;

		public void Reset()
		{
			Channels.Clear();
			Clients.Clear();
			ServerGroups.Clear();
			ChannelGroups.Clear();
			OwnClientId = ClientId.Null;
			Server = new Server();

			OwnClient = null;
			OwnChannel = null;
		}

		// Manual post event functions

		partial void PostClientEnterView(ClientEnterView msg)
		{
			var clientId = msg.ClientId;
			if (clientId == OwnClientId && OwnClient is null)
			{
				OwnClient = GetClient(OwnClientId);
				if (OwnClient is null) Log.Warn("Own client enterd but was not found in Book");
			}

			SetOwnChannelSubscribed(clientId);
		}
		partial void PostClientMoved(ClientMoved msg) => SetOwnChannelSubscribed(msg.ClientId);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void SetOwnChannelSubscribed(ClientId clientId)
		{
			if (clientId == OwnClientId && OwnClient != null)
			{
				OwnChannel = GetChannel(OwnClient.Channel);
				if (OwnChannel != null)
				{
					OwnChannel.Subscribed = true;
				}
				else
				{
					Log.Warn("Switched channel but new channel was not found in Book");
				}
			}
		}

		// Manual move functions

		private static (MaxClients?, MaxClients?) MaxClientsCcFun(ChannelCreated msg) => MaxClientsFun(msg.MaxClients, msg.IsMaxClientsUnlimited, msg.MaxFamilyClients, msg.IsMaxFamilyClientsUnlimited, msg.InheritsMaxFamilyClients);
		private static (MaxClients?, MaxClients?) MaxClientsCeFun(ChannelEdited msg) => MaxClientsFun(msg.MaxClients, msg.IsMaxClientsUnlimited, msg.MaxFamilyClients, msg.IsMaxFamilyClientsUnlimited, msg.InheritsMaxFamilyClients);
		private static (MaxClients?, MaxClients?) MaxClientsClFun(ChannelList msg) => MaxClientsFun(msg.MaxClients, msg.IsMaxClientsUnlimited, msg.MaxFamilyClients, msg.IsMaxFamilyClientsUnlimited, msg.InheritsMaxFamilyClients);
		private static (MaxClients?, MaxClients?) MaxClientsFun(int? maxClients, bool? isMaxClientsUnlimited, int? maxFamilyClients, bool? isMaxFamilyClientsUnlimited, bool? inheritsMaxFamilyClients)
		{
			var chn = isMaxClientsUnlimited == true
				? MaxClients.Unlimited
				: new MaxClients(
					MaxClientsKind.Limited,
					(ushort)Math.Max(Math.Min(ushort.MaxValue, maxClients ?? ushort.MaxValue), 0));

			var fam = isMaxFamilyClientsUnlimited == true
				? MaxClients.Unlimited
				: inheritsMaxFamilyClients == true
				? MaxClients.Inherited
				: new MaxClients(
					MaxClientsKind.Limited,
					(ushort)Math.Max(Math.Min(ushort.MaxValue, maxFamilyClients ?? ushort.MaxValue), 0));

			return (chn, fam);
		}

		private static ChannelType ChannelTypeCcFun(ChannelCreated msg) => ChannelTypeFun(msg.IsSemiPermanent, msg.IsPermanent);
		private static ChannelType ChannelTypeCeFun(ChannelEdited msg) => ChannelTypeFun(msg.IsSemiPermanent, msg.IsPermanent);
		private static ChannelType ChannelTypeClFun(ChannelList msg) => ChannelTypeFun(msg.IsSemiPermanent, msg.IsPermanent);
		private static ChannelType ChannelTypeFun(bool? semi, bool? perma)
		{
			if (semi == true) return ChannelType.SemiPermanent;
			else if (perma == true) return ChannelType.Permanent;
			else return ChannelType.Temporary;
		}

		private static string? AwayCevFun(ClientEnterView msg) => AwayFun(msg.IsAway, msg.AwayMessage);
		private static string? AwayCuFun(ClientUpdated msg) => AwayFun(msg.IsAway, msg.AwayMessage);
		private static string? AwayFun(bool? away, string? msg)
			=> away switch
			{
				true => msg ?? "",
				_ => null,
			};

		private static TalkPowerRequest? TalkPowerCevFun(ClientEnterView msg)
		{
			if (msg.TalkPowerRequestTime != Tools.UnixTimeStart)
				return new TalkPowerRequest() { Time = msg.TalkPowerRequestTime, Message = msg.TalkPowerRequestMessage ?? "" };
			return null;
		}
		private static TalkPowerRequest? TalkPowerCuFun(ClientUpdated msg) => TalkPowerFun(msg.TalkPowerRequestTime, msg.TalkPowerRequestMessage);
		private static TalkPowerRequest? TalkPowerFun(DateTime? time, string? message)
		{
			if (time != null && time != Tools.UnixTimeStart) // TODO
				return new TalkPowerRequest() { Time = time.Value, Message = message ?? "" };
			return null;
		}

		private static ClientType ClientTypeCevFun(ClientEnterView msg) => msg.ClientType;

		private ChannelId ChannelOrderCcFun(ChannelCreated msg)
		{
			ChannelOrderInsert(msg.ChannelId, msg.Order, msg.ParentId);
			return msg.Order;
		}
		private ChannelId ChannelOrderCmFun(ChannelMoved msg) => ChannelOrderMoveFun(msg.ChannelId, msg.Order, msg.ParentId);
		private ChannelId? ChannelOrderCeFun(ChannelEdited msg)
		{
			if (msg.Order == null)
				return null;
			return ChannelOrderMoveFun(msg.ChannelId, msg.Order.Value, msg.ParentId);
		}

		private ChannelId ChannelOrderMoveFun(ChannelId id, ChannelId newOrder, ChannelId? parent)
		{
			// [ C:4 | O:0 ]
			// [ C:5 | O:4 ]──┐
			// [ C:7 | O:5 ]  │ (Up1: O -> 4)
			// [            <─┘ (Chg: C:5 | O:7)
			// [ C:8 | O:7 ]    (Up2: O -> 5)

			var cur = Channels[id];
			var oldOrder = cur.Order;
			var newParent = parent ?? cur.Parent;

			ChannelOrderRemove(id, oldOrder); // Up1
			ChannelOrderInsert(id, newOrder, newParent); // Up2
			return newOrder;
		}

		private void ChannelOrderRemove(ChannelId id, ChannelId oldOrder)
		{
			// [ C:7 | O:_ ]
			// [ C:5 | O:7 ] ─>X
			// [ C:_ | O:5 ]     (Upd: O -> 7)

			var chan = Channels.Values.FirstOrDefault(x => x.Order == id);
			if (chan != null) chan.Order = oldOrder;
		}

		private void ChannelOrderInsert(ChannelId id, ChannelId newOrder, ChannelId parent)
		{
			// [ C:7 | O:_ ]
			// [            <── (New: C:5 | O:7)
			// [ C:_ | O:7 ]    (Upd: O -> 5)

			// or

			// [ C:_ | O:_ ]     
			//  ├ [            <── (New: C:5 | O:0)
			//  └ [ C:_ | O:0 ]    (Upd: O -> 5)

			// Multiple channel with Order:0 might exist,
			// we need to find one with the same parent as the inserted channel
			var chan = Channels.Values.FirstOrDefault(x => x.Order == newOrder && x.Parent == parent);
			if (chan != null) chan.Order = id;
		}

		private static Codec ChannelCodecCcFun(ChannelCreated msg) => msg.Codec ?? Codec.OpusVoice;

		private static SocketAddr? AddressFun(ClientConnectionInfo msg) => msg.Ip;

		private void SetClientDataFun(InitServer initServer)
		{
			OwnClientId = initServer.ClientId;
		}

		private static bool ChannelSubscribeFun(ChannelSubscribed _) => true;
		private bool ChannelUnsubscribeFun(ChannelUnsubscribed msg)
		{
			var goneClients = Clients.Values.Where(client => client.Channel == msg.ChannelId).ToArray();
			foreach (var clid in goneClients)
				Clients.Remove(clid.Id);
			return false;
		}

		private void SubscribeChannelFun(ClientMoved msg)
		{
			if (msg.ClientId == OwnClientId && Channels.TryGetValue(msg.TargetChannelId, out var channel))
				channel.Subscribed = true;
		}

		private static bool ReturnFalse<T>(T _) => false;
		private static object? ReturnSomeNone<T>(T _) => null;
	}
}
