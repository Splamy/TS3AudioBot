// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using TSLib.Commands;
using TSLib.Full.Book;
using TSLib.Messages;
using CmdR = System.Threading.Tasks.Task<System.E<TSLib.Messages.CommandError>>;

namespace TSLib
{
	public delegate void NotifyEventHandler<in TEventArgs>(object sender, IEnumerable<TEventArgs> e) where TEventArgs : INotification;

	/// <summary>A shared function base between the query and full client.</summary>
	public abstract partial class TsBaseFunctions : IDisposable
	{
		protected readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		/// <summary>After the client disconnected.</summary>
		public abstract event EventHandler<DisconnectEventArgs> OnDisconnected;

		/// <summary>Get whether this client is currently connected.</summary>
		public abstract bool Connected { get; }
		/// <summary>Get whether this client is currently trying to connect.</summary>
		public abstract bool Connecting { get; }
		/// <summary>The derived client type.</summary>
		public abstract ClientType ClientType { get; }
		/// <summary>The connection data this client was last connected with (or is currently connected to).</summary>
		public ConnectionData? ConnectionData { get; protected set; }
		internal IPEndPoint? remoteAddress;
		private ushort transferIdCnt;
		protected abstract Deserializer Deserializer { get; }
		public TsConst ServerConstants { get; protected set; } = TsConst.Default;

		public abstract CmdR Connect(ConnectionData conData);
		public abstract Task Disconnect();
		public abstract void Dispose();

		#region NETWORK SEND
		/// <summary>Sends a command to the server. Commands look exactly like query commands and mostly also behave identically.</summary>
		/// <typeparam name="T">The type to deserialize the response to. Use <see cref="ResponseDictionary"/> for unknown response data.</typeparam>
		/// <param name="com">The raw command to send.</param>
		/// <returns>Returns an enumeration of the deserialized and split up in <see cref="T"/> objects data.</returns>
		public abstract Task<R<T[], CommandError>> Send<T>(TsCommand com) where T : IResponse, new();

		/// <summary>
		/// Sends a command and depending on the client type waits for a response or notification.
		/// <para>NOTE: Do not use this method unless you are sure the ts command fits the criteria.</para>
		/// </summary>
		/// <param name="command">The command to send.</param>
		/// <param name="type">The notification type to wait for and serialize to.</param>
		public abstract Task<R<T[], CommandError>> SendHybrid<T>(TsCommand com, NotificationType type) where T : class, IResponse, new();
		#endregion

		private string? GenPassword(string? password)
		{
			if (ClientType == ClientType.Full && password != null)
				return Full.TsCrypt.HashPassword(password);
			else
				return password;
		}

		#region UNIVERSAL COMMANDS

		public CmdR ChangeName(string newName)
			=> SendVoid(new TsCommand("clientupdate") {
				{ "client_nickname", newName },
			});

		public CmdR ChangeBadges(string newBadges)
			=> SendVoid(new TsCommand("clientupdate") {
				{ "client_badges", newBadges },
			});

		public CmdR ChangeDescription(string newDescription, ClientId clientId)
			=> SendVoid(new TsCommand("clientedit") {
				{ "clid", clientId },
				{ "client_description", newDescription },
			});

		/// <summary>Displays information about your current ServerQuery connection including your loginname, etc.</summary>
		public Task<R<WhoAmI, CommandError>> WhoAmI() // Q ?
			=> Send<WhoAmI>("whoami").MapToSingle();

		public CmdR SendPrivateMessage(string message, ClientId clientId)
			=> SendMessage(message, TextMessageTargetMode.Private, clientId.Value);

		public CmdR SendChannelMessage(string message)
			=> SendMessage(message, TextMessageTargetMode.Channel, 0);

		public CmdR SendServerMessage(string message, ulong serverId)
			=> SendMessage(message, TextMessageTargetMode.Server, serverId);

		/// <summary>Sends a text message to a specified target.
		/// If targetmode is set to <see cref="TextMessageTargetMode.Private"/>, a message is sent to the client with the ID specified by target.
		/// If targetmode is set to <see cref="TextMessageTargetMode.Channel"/> or <see cref="TextMessageTargetMode.Server"/>,
		/// the target parameter will be ignored and a message is sent to the current channel or server respectively.</summary>
		public CmdR SendMessage(string message, TextMessageTargetMode target, ulong id)
			=> SendVoid(new TsCommand("sendtextmessage") {
				{ "targetmode", (int)target },
				{ "target", id },
				{ "msg", message },
			});

		/// <summary>Sends a text message to all clients on all virtual servers in the TeamSpeak 3 Server instance.</summary>
		public CmdR SendGlobalMessage(string message)
			=> SendVoid(new TsCommand("gm") {
				{ "msg", message },
			});

		public CmdR KickClientFromServer(ClientId clientId, string? reasonMsg = null)
			=> KickClient(new[] { clientId }, ReasonIdentifier.Server, reasonMsg);

		public CmdR KickClientFromServer(ClientId[] clientIds, string? reasonMsg = null)
			=> KickClient(clientIds, ReasonIdentifier.Server, reasonMsg);

		public CmdR KickClientFromChannel(ClientId clientId, string? reasonMsg = null)
			=> KickClient(new[] { clientId }, ReasonIdentifier.Channel, reasonMsg);

		public CmdR KickClientFromChannel(ClientId[] clientIds, string? reasonMsg = null)
			=> KickClient(clientIds, ReasonIdentifier.Channel, reasonMsg);

		/// <summary>Kicks one or more clients specified with clid from their currently joined channel or from the server, depending on <paramref name="reasonId"/>.
		/// The reasonmsg parameter specifies a text message sent to the kicked clients.
		/// This parameter is optional and may only have a maximum of 40 characters.</summary>
		public CmdR KickClient(ClientId[] clientIds, ReasonIdentifier reasonId, string? reasonMsg = null)
			=> SendVoid(new TsCommand("clientkick") {
				{ "reasonid", (int)reasonId },
				{ "clid", clientIds },
				{ "reasonmsg", reasonMsg },
			});

		public CmdR BanClient(ushort clientId, TimeSpan? duration = null, string? reasonMsg = null)
			=> BanClient(new CommandParameter("clid", clientId), reasonMsg, duration);

		public CmdR BanClient(Uid clientUid = default, TimeSpan? duration = null, string? reasonMsg = null)
			=> BanClient(new CommandParameter("uid", clientUid), reasonMsg, duration);

		private CmdR BanClient(ICommandPart clientIdentifier, string? reasonMsg = null, TimeSpan? duration = null)
			=> SendVoid(new TsCommand("banclient") {
				clientIdentifier,
				{ "banreason", reasonMsg },
				{ "time", duration?.TotalSeconds },
			});

		public CmdR ChannelEdit(ChannelId channelId,
			string? name = null, string? namePhonetic = null, string? topic = null, string? description = null,
			string? password = null, Codec? codec = null, int? codecQuality = null, int? codecLatencyFactor = null,
			bool? codecEncrypted = null, int? maxClients = null, int? maxFamilyClients = null, bool? maxClientsUnlimited = null,
			bool? maxFamilyClientsUnlimited = null, bool? maxFamilyClientsInherited = null, ChannelId? order = null,
			ChannelType? type = null, TimeSpan? deleteDelay = null, int? neededTalkPower = null)
			=> SendVoid(ChannelOp("channeledit", channelId, name, namePhonetic, topic, description,
				password, codec, codecQuality, codecLatencyFactor, codecEncrypted,
				maxClients, maxFamilyClients, maxClientsUnlimited, maxFamilyClientsUnlimited,
				maxFamilyClientsInherited, order, null, type, deleteDelay, neededTalkPower));

		public abstract Task<R<IChannelCreateResponse, CommandError>> ChannelCreate(string name,
			string? namePhonetic = null, string? topic = null, string? description = null, string? password = null,
			Codec? codec = null, int? codecQuality = null, int? codecLatencyFactor = null, bool? codecEncrypted = null,
			int? maxClients = null, int? maxFamilyClients = null, bool? maxClientsUnlimited = null,
			bool? maxFamilyClientsUnlimited = null, bool? maxFamilyClientsInherited = null, ChannelId? order = null,
			ChannelId? parent = null, ChannelType? type = null, TimeSpan? deleteDelay = null, int? neededTalkPower = null);

		protected TsCommand ChannelOp(string op, ChannelId? channelId,
			string? name, string? namePhonetic, string? topic, string? description, string? password,
			Codec? codec, int? codecQuality, int? codecLatencyFactor, bool? codecEncrypted, int? maxClients,
			int? maxFamilyClients, bool? maxClientsUnlimited, bool? maxFamilyClientsUnlimited, bool? maxFamilyClientsInherited,
			ChannelId? order, ChannelId? parent, ChannelType? type, TimeSpan? deleteDelay, int? neededTalkPower)
			=> new TsCommand(op) {
				{ "cid", channelId },
				{ "cpid", parent },
				{ "channel_name", name },
				{ "channel_name_phonetic", namePhonetic },
				{ "channel_topic", topic },
				{ "channel_description", description },
				{ "channel_password", GenPassword(password) },
				{ "channel_codec", (byte?)codec },
				{ "channel_codec_quality", codecQuality },
				{ "channel_codec_latency_factor", codecLatencyFactor },
				{ "channel_codec_is_unencrypted", !codecEncrypted },
				{ "channel_maxclients", maxClients },
				{ "channel_maxfamilyclients", maxFamilyClients },
				{ "channel_flag_maxclients_unlimited", maxClientsUnlimited },
				{ "channel_flag_maxfamilyclients_unlimited", maxFamilyClientsUnlimited },
				{ "channel_flag_maxfamilyclients_inherited", maxFamilyClientsInherited },
				{ "channel_order", order },
				{ "channel_flag_permanent", type == null ? (bool?)null : type == ChannelType.Permanent },
				{ "channel_flag_semi_permanent", type == null ? (bool?)null : type == ChannelType.SemiPermanent },
				{ "channel_delete_delay", (ulong?)deleteDelay?.TotalSeconds }, // TODO Check
				{ "channel_needed_talk_power", neededTalkPower },
			};

		/// <summary>Displays detailed configuration information about a channel including ID, topic, description, etc.
		/// For detailed information, see Channel Properties.</summary>
		public Task<R<ChannelInfoResponse[], CommandError>> ChannelInfo(ChannelId channelId)
			=> Send<ChannelInfoResponse>(new TsCommand("channelinfo") {
				{ "cid", channelId },
			});

		/// <summary>Displays a list of channels created on a virtual server including their ID, order, name, etc.
		/// The output can be modified using several command options.</summary>
		public Task<R<ChannelListResponse[], CommandError>> ChannelList(ChannelListOptions options = 0)
			=> Send<ChannelListResponse>("channellist",
			new CommandOption(options));

		public CmdR ChannelMove(ChannelId channelId, ChannelId? parent = null, ChannelId? order = null)
			=> SendVoid(new TsCommand("channelmove") {
				{ "cid", channelId },
				{ "cpid", parent },
				{ "order", order },
			});

		public CmdR ChannelDelete(ChannelId channelId, bool force = false)
			=> SendVoid(new TsCommand("channeldelete") {
				{ "cid", channelId },
				{ "force", force },
			});

		/// <summary>Displays detailed database information about a client including unique ID, creation date, etc.</summary>
		public Task<R<ClientDbInfo, CommandError>> ClientDbInfo(ClientDbId clientDbId)
			=> Send<ClientDbInfo>(new TsCommand("clientdbinfo") {
				{ "cldbid", clientDbId },
			}).MapToSingle();

		/// <summary>Displays a list of clients online on a virtual server including their ID, nickname, status flags, etc.
		/// The output can be modified using several command options.
		/// Please note that the output will only contain clients which are currently in channels you're able to subscribe to.</summary>
		public Task<R<ClientList[], CommandError>> ClientList(ClientListOptions options = 0)
			=> Send<ClientList>("clientlist",
			new CommandOption(options));

		/// <summary>Displays detailed configuration information about a client including unique ID, nickname, client version, etc.</summary>
		public Task<R<ClientInfo, CommandError>> ClientInfo(ClientId clientId)
			=> Send<ClientInfo>(new TsCommand("clientinfo") {
				{ "clid", clientId },
			}).MapToSingle();

		/// <summary>Use a token key and gain access to a server or channel group.
		/// Please note that the server will automatically delete the token after it has been used.</summary>
		public CmdR PrivilegeKeyUse(string key)
			=> SendVoid(new TsCommand("privilegekeyuse") {
				{ "token", key },
			});

		/// <summary>Adds a set of specified permissions to the server group specified with <paramref name="serverGroupId"/>.
		/// Multiple permissions can be added by providing the four parameters of each permission.</summary>
		public CmdR ServerGroupAddPerm(ServerGroupId serverGroupId, TsPermission permission, int permissionValue,
				bool permissionNegated, bool permissionSkip)
			=> SendVoid(new TsCommand("servergroupaddperm") {
				{ "sgid", serverGroupId },
				{ "permvalue", permissionValue },
				{ "permnegated", permissionNegated },
				{ "permskip", permissionSkip },
				TsPermissionHelper.GetAsParameter(Deserializer.PermissionTransform, permission),
			});

		/// <summary>Adds a set of specified permissions to the server group specified with <paramref name="serverGroupId"/>.
		/// Multiple permissions can be added by providing the four parameters of each permission.</summary>
		public CmdR ServerGroupAddPerm(ServerGroupId serverGroupId, TsPermission[] permission, int[] permissionValue,
				bool[] permissionNegated, bool[] permissionSkip)
			=> SendVoid(new TsCommand("servergroupaddperm") {
				{ "sgid", serverGroupId },
				{ "permvalue", permissionValue },
				{ "permnegated", permissionNegated },
				{ "permskip", permissionSkip },
				TsPermissionHelper.GetAsMultiParameter(Deserializer.PermissionTransform, permission),
			});

		/// <summary>Adds a client to the server group specified with <paramref name="serverGroupId"/>. Please note that a
		/// client cannot be added to default groups or template groups.</summary>
		public CmdR ServerGroupAddClient(ServerGroupId serverGroupId, ClientDbId clientDbId)
			=> SendVoid(new TsCommand("servergroupaddclient") {
				{ "sgid", serverGroupId },
				{ "cldbid", clientDbId },
			});

		/// <summary>Removes a client specified with cldbid from the server group specified with <paramref name="serverGroupId"/>.</summary>
		public CmdR ServerGroupDelClient(ServerGroupId serverGroupId, ClientDbId clientDbId)
			=> SendVoid(new TsCommand("servergroupdelclient") {
				{ "sgid", serverGroupId },
				{ "cldbid", clientDbId },
			});

		public CmdR FileTransferStop(ushort serverTransferId, bool delete)
			=> SendVoid(new TsCommand("ftstop") {
				{ "serverftfid", serverTransferId },
				{ "delete", delete },
			});

		public CmdR FileTransferDeleteFile(ChannelId channelId, string[] path, string channelPassword = "")
			=> SendVoid(new TsCommand("ftdeletefile") {
				{ "cid", channelId },
				{ "cpw", channelPassword },
				{ "name", path },
			});

		public CmdR FileTransferCreateDirectory(ChannelId channelId, string path, string channelPassword = "")
			=> SendVoid(new TsCommand("ftcreatedir") {
				{ "cid", channelId },
				{ "dirname", path },
				{ "cpw", channelPassword },
			});

		public CmdR FileTransferRenameFile(ChannelId channelId, string oldName, string channelPassword, string newName,
				ChannelId? targetChannel = null, string targetChannelPassword = "")
			=> SendVoid(new TsCommand("ftrenamefile") {
				{ "cid", channelId },
				{ "oldname", oldName },
				{ "newname", newName },
				{ "cpw", channelPassword },
				{ "tcid", targetChannel },
				{ "tcpw", targetChannel.HasValue ? targetChannelPassword : null },
			});

		public async CmdR UploadAvatar(System.IO.Stream image)
		{
			var token = await UploadFile(image, ChannelId.Null, "/avatar", overwrite: true, createMd5: true);
			if (!token.Ok)
				return CommandError.Custom("Avatar upload failed: " + token.Error.ErrorFormat());
			if (token.Value.Status != TransferStatus.Done)
				return CommandError.Custom("Avatar upload failed");
			var md5 = string.Concat(token.Value.Md5Sum.Select(x => x.ToString("x2")));
			return await SendVoid(new TsCommand("clientupdate") { { "client_flag_avatar", md5 } });
		}

		/// <summary>Deletes the avatar of a user.
		/// Can be called without uid to delete own avatar.</summary>
		/// <param name="clientUid">The client uid where the avatar should be deleted.</param>
		public CmdR DeleteAvatar(Uid? clientUid = null)
		{
			string path = "/avatar_" + clientUid;
			return FileTransferDeleteFile(ChannelId.Null, new[] { path });
		}

		public CmdR ClientMove(ClientId clientId, ChannelId channelId, string? channelPassword = null)
			=> SendVoid(new TsCommand("clientmove") {
				{ "clid", clientId },
				{ "cid", channelId },
				{ "cpw", GenPassword(channelPassword) },
			});

		#endregion

		#region UNIVERSAL HYRBRID COMMANDS

		/// <summary>Creates a new server group using the name specified with <paramref name="name"/> and return its ID.
		/// The optional <paramref name="type"/> parameter can be used to create ServerQuery groups and template groups.</summary>
		public abstract Task<R<ServerGroupAddResponse, CommandError>> ServerGroupAdd(string name, GroupType? type = null);

		/// <summary>Displays all server groups the client specified with <paramref name="clDbId"/> is currently residing in.</summary>
		public Task<R<ServerGroupsByClientId[], CommandError>> ServerGroupsByClientDbId(ClientDbId clDbId)
			=> SendHybrid<ServerGroupsByClientId>(new TsCommand("servergroupsbyclientid")
			{
				{ "cldbid", clDbId }
			}, NotificationType.ServerGroupsByClientId);

		public abstract Task<R<FileUpload, CommandError>> FileTransferInitUpload(ChannelId channelId, string path, string channelPassword,
			ushort clientTransferId, long fileSize, bool overwrite, bool resume);

		public abstract Task<R<FileDownload, CommandError>> FileTransferInitDownload(ChannelId channelId, string path, string channelPassword,
			ushort clientTransferId, long seek);

		public Task<R<FileTransfer[], CommandError>> FileTransferList()
			=> SendHybrid<FileTransfer>(new TsCommand("ftlist"),
				NotificationType.FileTransfer);

		public Task<R<FileList[], CommandError>> FileTransferGetFileList(ChannelId channelId, string path, string channelPassword = "")
			=> SendHybrid<FileList>(new TsCommand("ftgetfilelist") {
				{ "cid", channelId },
				{ "path", path },
				{ "cpw", channelPassword } // TODO CHECK ?
			}, NotificationType.FileList);

		public Task<R<FileInfo[], CommandError>> FileTransferGetFileInfo(ChannelId channelId, string[] path, string channelPassword = "")
			=> SendHybrid<FileInfo>(new TsCommand("ftgetfileinfo") {
				{ "cid", channelId },
				{ "cpw", channelPassword }, // TODO CHECK ?
				{ "name", path }
			}, NotificationType.FileInfo);

		public Task<R<ClientDbIdFromUid, CommandError>> GetClientDbIdFromUid(Uid clientUid)
			=> SendHybrid<ClientDbIdFromUid>(new TsCommand("clientgetdbidfromuid") {
				{ "cluid", clientUid }
			}, NotificationType.ClientDbIdFromUid).MapToSingle();

		public Task<R<ClientUidFromClid, CommandError>> GetClientUidFromClientId(ClientId clientId)
			=> SendHybrid<ClientUidFromClid>(new TsCommand("clientgetuidfromclid") {
				{ "clid", clientId }
			}, NotificationType.ClientUidFromClid).MapToSingle();

		public Task<R<ClientNameFromUid, CommandError>> GetClientNameFromUid(Uid clientUid)
			=> SendHybrid<ClientNameFromUid>(new TsCommand("clientgetnamefromuid")
			{
				{ "cluid", clientUid }
			}, NotificationType.ClientNameFromUid).MapToSingle();

		public Task<R<ClientIds[], CommandError>> GetClientIds(Uid clientUid)
			=> SendHybrid<ClientIds>(new TsCommand("clientgetids") {
				{ "cluid", clientUid }
			}, NotificationType.ClientIds);

		public Task<R<PermOverview[], CommandError>> PermOverview(ClientDbId clientDbId, ChannelId channelId, params TsPermission[] permission)
			=> SendHybrid<PermOverview>(new TsCommand("permoverview") {
				{ "cldbid", clientDbId },
				{ "cid", channelId },
				TsPermissionHelper.GetAsMultiParameter(Deserializer.PermissionTransform, permission)
			}, NotificationType.PermOverview);

		public Task<R<PermList[], CommandError>> PermissionList()
			=> SendHybrid<PermList>(new TsCommand("permissionlist"),
				NotificationType.PermList);

		public Task<R<ServerConnectionInfo, CommandError>> GetServerConnectionInfo()
			=> SendHybrid<ServerConnectionInfo>(new TsCommand("serverrequestconnectioninfo"),
				NotificationType.ServerConnectionInfo).MapToSingle();

		public Task<R<ServerGroupClientList[], CommandError>> ServerGroupClientList(ServerGroupId serverGroupId, bool getNames = false)
			=> SendHybrid<ServerGroupClientList>(new TsCommand("servergroupclientlist")
			{
				{ "sgid", serverGroupId },
				{ getNames ? new CommandOption("names") : null }
			}, NotificationType.ServerGroupClientList);

		#endregion
	}
}
