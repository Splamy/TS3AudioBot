// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistringibute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Linq;
using TSLib.Helper;
using TSLib.Messages;
using SocketAddr = System.String;

namespace TSLib.Full.Book
{
	public partial class Connection
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public Client Self() => GetClient(OwnClient);
		public Channel CurrentChannel()
		{
			var self = Self();
			if (self == null)
				return null;
			return GetChannel(self.Channel);
		}

		private void SetServer(Server server)
		{
			Server = server;
		}

		private Channel GetChannel(ChannelId id)
		{
			if (Channels.TryGetValue(id, out var channel))
				return channel;
			return null;
		}

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

		private Client GetClient(ClientId id)
		{
			if (Clients.TryGetValue(id, out var client))
				return client;
			return null;
		}

		private void SetClient(Client client, ClientId id)
		{
			client.Id = id;
			Clients[id] = client;
		}

		private void RemoveClient(ClientId id)
		{
			Clients.Remove(id);
		}

		private void SetConnectionClientData(ConnectionClientData connectionClientData, ClientId id)
		{
			if (!Clients.TryGetValue(id, out var client))
				return;
			client.ConnectionData = connectionClientData;
		}

		private void SetServerGroup(ServerGroup serverGroup, ServerGroupId id)
		{
			Groups[id] = serverGroup;
		}

		private Server GetServer()
		{
			return Server;
		}

		// Manual post event functions

		partial void PostClientEnterView(ClientEnterView msg) => SetOwnChannelSubscribed(msg.ClientId);
		partial void PostClientMoved(ClientMoved msg) => SetOwnChannelSubscribed(msg.ClientId);
		private void SetOwnChannelSubscribed(ClientId clientId)
		{
			if (clientId == OwnClient)
			{
				var curChan = CurrentChannel();
				if (curChan != null)
				{
					curChan.Subscribed = true;
				}
			}
		}

		// Manual move functions

		private static (MaxClients?, MaxClients?) MaxClientsCcFun(ChannelCreated msg) => MaxClientsFun(msg.MaxClients, msg.IsMaxClientsUnlimited, msg.MaxFamilyClients, msg.IsMaxFamilyClientsUnlimited, msg.InheritsMaxFamilyClients);
		private static (MaxClients?, MaxClients?) MaxClientsCeFun(ChannelEdited msg) => MaxClientsFun(msg.MaxClients, msg.IsMaxClientsUnlimited, msg.MaxFamilyClients, msg.IsMaxFamilyClientsUnlimited, msg.InheritsMaxFamilyClients);
		private static (MaxClients?, MaxClients?) MaxClientsClFun(ChannelList msg) => MaxClientsFun(msg.MaxClients, msg.IsMaxClientsUnlimited, msg.MaxFamilyClients, msg.IsMaxFamilyClientsUnlimited, msg.InheritsMaxFamilyClients);
		private static (MaxClients?, MaxClients?) MaxClientsFun(int? MaxClients, bool? IsMaxClientsUnlimited, int? MaxFamilyClients, bool? IsMaxFamilyClientsUnlimited, bool? InheritsMaxFamilyClients)
		{
			var chn = new MaxClients();
			if (IsMaxClientsUnlimited == true) chn.LimitKind = MaxClientsKind.Unlimited;
			else
			{
				chn.LimitKind = MaxClientsKind.Limited;
				chn.Count = (ushort)Math.Max(Math.Min(ushort.MaxValue, MaxClients ?? ushort.MaxValue), 0);
			}

			var fam = new MaxClients();
			if (IsMaxFamilyClientsUnlimited == true) fam.LimitKind = MaxClientsKind.Unlimited;
			else if (InheritsMaxFamilyClients == true) fam.LimitKind = MaxClientsKind.Inherited;
			else
			{
				fam.LimitKind = MaxClientsKind.Limited;
				fam.Count = (ushort)Math.Max(Math.Min(ushort.MaxValue, MaxFamilyClients ?? ushort.MaxValue), 0);
			}
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

		private string AwayCevFun(ClientEnterView msg) => AwayFun(msg.IsAway, msg.AwayMessage);
		private string AwayCuFun(ClientUpdated msg) => AwayFun(msg.IsAway, msg.AwayMessage);
		private string AwayFun(bool? away, string msg)
		{
			if (away == true)
				return msg ?? "";
			if (away == false)
				return "";
			return null;
		}

		private static TalkPowerRequest? TalkPowerCevFun(ClientEnterView msg)
		{
			if (msg.TalkPowerRequestTime != Tools.UnixTimeStart)
				return new TalkPowerRequest() { Time = msg.TalkPowerRequestTime, Message = msg.TalkPowerRequestMessage ?? "" };
			return null;
		}
		private static TalkPowerRequest? TalkPowerCuFun(ClientUpdated msg) => TalkPowerFun(msg.TalkPowerRequestTime, msg.TalkPowerRequestMessage);
		private static TalkPowerRequest? TalkPowerFun(DateTime? time, string message)
		{
			if (time != null && time != Tools.UnixTimeStart) // TODO
				return new TalkPowerRequest() { Time = time.Value, Message = message ?? "" };
			return null;
		}

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

		private static SocketAddr AddressFun(ClientConnectionInfo msg) => msg.Ip;

		private void SetClientDataFun(InitServer initServer)
		{
			OwnClient = initServer.ClientId;
		}

		private bool ChannelSubscribeFun(ChannelSubscribed _) => true;

		private bool ChannelUnsubscribeFun(ChannelUnsubscribed msg)
		{
			var goneClients = Clients.Values.Where(client => client.Channel == msg.ChannelId).ToArray();
			foreach (var clid in goneClients)
				Clients.Remove(clid.Id);
			return false;
		}

		private static bool ReturnFalse<T>(T _) => false;
	}
}
