// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client
{
	using Commands;
	using Helper;
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net;
	using ChannelIdT = System.UInt64;
	using ClientDbIdT = System.UInt64;
	using ClientIdT = System.UInt16;
	using CmdR = System.E<Messages.CommandError>;
	using ServerGroupIdT = System.UInt64;
	using Uid = System.String;

	public delegate void NotifyEventHandler<in TEventArgs>(object sender, IEnumerable<TEventArgs> e) where TEventArgs : INotification;

	/// <summary>A shared function base between the query and full client.</summary>
	public abstract class Ts3BaseFunctions : IDisposable
	{
		protected static readonly NLog.Logger LogCmd = NLog.LogManager.GetLogger("TS3Client.Cmd");
		/// <summary>When this client receives any visible message.</summary>
		public abstract event NotifyEventHandler<TextMessage> OnTextMessage;
		/// <summary>When another client enters visiblility.</summary>
		public abstract event NotifyEventHandler<ClientEnterView> OnClientEnterView;
		/// <summary>When another client leaves visiblility.</summary>
		public abstract event NotifyEventHandler<ClientLeftView> OnClientLeftView;
		/// <summary>After the client connected.</summary>
		public abstract event EventHandler<EventArgs> OnConnected;
		/// <summary>After the client disconnected.</summary>
		public abstract event EventHandler<DisconnectEventArgs> OnDisconnected;

		/// <summary>Get whether this client is currently connected.</summary>
		public abstract bool Connected { get; }
		/// <summary>Get whether this client is currently trying to connect.</summary>
		public abstract bool Connecting { get; }
		/// <summary>The derived client type.</summary>
		public abstract ClientType ClientType { get; }
		/// <summary>The connection data this client was last connected with (or is currently connected to).</summary>
		public ConnectionData ConnectionData { get; protected set; }
		internal IPEndPoint remoteAddress;
		private FileTransferManager ftm;
		/// <summary>An instance to a <see cref="FileTransferManager"/> dedicated for this client.</summary>
		public FileTransferManager FileTransferManager => ftm ?? (ftm = new FileTransferManager(this));
		protected abstract Deserializer Deserializer { get; }

		public abstract void Connect(ConnectionData conData);
		public abstract void Disconnect();
		public abstract void Dispose();

		#region NETWORK SEND

		/// <summary>Creates a new command.</summary>
		/// <param name="command">The command name.</param>
		public R<ResponseDictionary[], CommandError> Send(string command)
			=> SendCommand<ResponseDictionary>(new Ts3Command(command));

		/// <summary>Creates a new command.</summary>
		/// <param name="command">The command name.</param>
		/// <param name="parameter">The parameters to be added to this command.
		/// See <see cref="CommandParameter"/>, <see cref="CommandOption"/> or <see cref="CommandMultiParameter"/> for more information.</param>
		public R<ResponseDictionary[], CommandError> Send(string command, params ICommandPart[] parameter)
			=> SendCommand<ResponseDictionary>(new Ts3Command(command, parameter.ToList()));

		/// <summary>Creates a new command.</summary>
		/// <typeparam name="T">The type to deserialize the response to.</typeparam>
		/// <param name="command">The command name.</param>
		/// <returns>Returns an enumeration of the deserialized and split up in <see cref="T"/> objects data.</returns>
		public R<T[], CommandError> Send<T>(string command) where T : IResponse, new()
			=> SendCommand<T>(new Ts3Command(command));

		/// <summary>Creates a new command.</summary>
		/// <typeparam name="T">The type to deserialize the response to.</typeparam>
		/// <param name="command">The command name.</param>
		/// <param name="parameter">The parameters to be added to this command.</param>
		/// <returns>Returns an enumeration of the deserialized and split up in <see cref="T"/> objects data.</returns>
		public R<T[], CommandError> Send<T>(string command, params ICommandPart[] parameter) where T : IResponse, new()
			=> Send<T>(command, parameter.ToList());

		/// <summary>Creates a new command.</summary>
		/// <typeparam name="T">The type to deserialize the response to.</typeparam>
		/// <param name="command">The command name.</param>
		/// <param name="parameter">The parameters to be added to this command.</param>
		/// <returns>Returns an enumeration of the deserialized and split up in <see cref="T"/> objects data.</returns>
		public R<T[], CommandError> Send<T>(string command, List<ICommandPart> parameter) where T : IResponse, new()
			=> SendCommand<T>(new Ts3Command(command, parameter));

		protected CmdR SendNoResponsed(Ts3Command command)
			=> SendCommand<ResponseVoid>(command.ExpectsResponse(false));

		/// <summary>Sends a command to the server. Commands look exactly like query commands and mostly also behave identically.</summary>
		/// <typeparam name="T">The type to deserialize the response to. Use <see cref="ResponseDictionary"/> for unknown response data.</typeparam>
		/// <param name="com">The raw command to send.</param>
		/// <returns>Returns an enumeration of the deserialized and split up in <see cref="T"/> objects data.</returns>
		public abstract R<T[], CommandError> SendCommand<T>(Ts3Command com) where T : IResponse, new();

		#endregion

		#region UNIVERSAL COMMANDS

		public CmdR ChangeName(string newName)
			=> SendCommand<ResponseVoid>(new Ts3Command("clientupdate") {
				{ "client_nickname", newName },
			});

		public CmdR ChangeBadges(string newBadges)
			=> SendCommand<ResponseVoid>(new Ts3Command("clientupdate") {
				{ "client_badges", newBadges },
			});

		public CmdR ChangeDescription(string newDescription, ClientIdT clientId)
			=> SendCommand<ResponseVoid>(new Ts3Command("clientedit") {
				{ "clid", clientId },
				{ "client_description", newDescription },
			});

		/// <summary>Displays information about your current ServerQuery connection including your loginname, etc.</summary>
		public R<WhoAmI, CommandError> WhoAmI() // Q ?
			=> Send<WhoAmI>("whoami").WrapSingle();

		public CmdR SendPrivateMessage(string message, ClientIdT clientId)
			=> SendMessage(message, TextMessageTargetMode.Private, clientId);

		public CmdR SendChannelMessage(string message)
			=> SendMessage(message, TextMessageTargetMode.Channel, 0);

		public CmdR SendServerMessage(string message, ulong serverId)
			=> SendMessage(message, TextMessageTargetMode.Server, serverId);

		/// <summary>Sends a text message to a specified target.
		/// If targetmode is set to <see cref="TextMessageTargetMode.Private"/>, a message is sent to the client with the ID specified by target.
		/// If targetmode is set to <see cref="TextMessageTargetMode.Channel"/> or <see cref="TextMessageTargetMode.Server"/>,
		/// the target parameter will be ignored and a message is sent to the current channel or server respectively.</summary>
		public CmdR SendMessage(string message, TextMessageTargetMode target, ulong id)
			=> SendCommand<ResponseVoid>(new Ts3Command("sendtextmessage") {
				{ "targetmode", (int)target },
				{ "target", id },
				{ "msg", message },
			});

		/// <summary>Sends a text message to all clients on all virtual servers in the TeamSpeak 3 Server instance.</summary>
		public CmdR SendGlobalMessage(string message)
			=> SendCommand<ResponseVoid>(new Ts3Command("gm") {
				{ "msg", message },
			});

		public CmdR KickClientFromServer(ClientIdT clientId, string reasonMsg = null)
			=> KickClient(new[] { clientId }, ReasonIdentifier.Server, reasonMsg);

		public CmdR KickClientFromServer(ClientIdT[] clientIds, string reasonMsg = null)
			=> KickClient(clientIds, ReasonIdentifier.Server, reasonMsg);

		public CmdR KickClientFromChannel(ClientIdT clientId, string reasonMsg = null)
			=> KickClient(new[] { clientId }, ReasonIdentifier.Channel, reasonMsg);

		public CmdR KickClientFromChannel(ClientIdT[] clientIds, string reasonMsg = null)
			=> KickClient(clientIds, ReasonIdentifier.Channel, reasonMsg);

		/// <summary>Kicks one or more clients specified with clid from their currently joined channel or from the server, depending on <paramref name="reasonId"/>.
		/// The reasonmsg parameter specifies a text message sent to the kicked clients.
		/// This parameter is optional and may only have a maximum of 40 characters.</summary>
		public CmdR KickClient(ClientIdT[] clientIds, ReasonIdentifier reasonId, string reasonMsg = null)
			=> SendCommand<ResponseVoid>(new Ts3Command("clientkick") {
				{ "reasonid", (int)reasonId },
				{ "clid", clientIds },
				{ "reasonmsg", reasonMsg },
			});

		public CmdR BanClient(ClientIdT clientId, TimeSpan? duration = null, string reasonMsg = null)
			=> BanClient(new CommandParameter("clid", clientId), reasonMsg, duration);

		public CmdR BanClient(Uid clientUid = null, TimeSpan? duration = null, string reasonMsg = null)
			=> BanClient(new CommandParameter("uid", clientUid), reasonMsg, duration);

		private CmdR BanClient(ICommandPart clientIdentifier, string reasonMsg = null, TimeSpan? duration = null)
			=> SendCommand<ResponseVoid>(new Ts3Command("banclient") {
				clientIdentifier,
				{ "banreason", reasonMsg },
				{ "time", duration?.TotalSeconds },
			});

		public R<ChannelData[], CommandError> ChannelList(ChannelListOptions options = 0)
			=> Send<ChannelData>("channellist",
			new CommandOption(options));

		/// <summary>Displays a list of clients online on a virtual server including their ID, nickname, status flags, etc.
		/// The output can be modified using several command options.
		/// Please note that the output will only contain clients which are currently in channels you're able to subscribe to.</summary>
		public R<ClientList[], CommandError> ClientList(ClientListOptions options = 0)
			=> Send<ClientList>("clientlist",
			new CommandOption(options));

		/// <summary>Displays detailed database information about a client including unique ID, creation date, etc.</summary>
		public R<ClientDbInfo, CommandError> ClientDbInfo(ClientDbIdT clDbId)
			=> Send<ClientDbInfo>("clientdbinfo",
			new CommandParameter("cldbid", clDbId)).WrapSingle();

		/// <summary>Displays detailed configuration information about a client including unique ID, nickname, client version, etc.</summary>
		public R<ClientInfo, CommandError> ClientInfo(ClientIdT clientId)
			=> Send<ClientInfo>("clientinfo",
			new CommandParameter("clid", clientId)).WrapSingle();

		/// <summary>Use a token key and gain access to a server or channel group.
		/// Please note that the server will automatically delete the token after it has been used.</summary>
		public CmdR PrivilegeKeyUse(string key)
			=> SendCommand<ResponseVoid>(new Ts3Command("privilegekeyuse") {
				{ "token", key },
			});

		/// <summary>Adds a set of specified permissions to the server group specified with <paramref name="serverGroupId"/>.
		/// Multiple permissions can be added by providing the four parameters of each permission.</summary>
		public CmdR ServerGroupAddPerm(ServerGroupIdT serverGroupId, Ts3Permission permission, int permissionValue,
				bool permissionNegated, bool permissionSkip)
			=> SendCommand<ResponseVoid>(new Ts3Command("servergroupaddperm") {
				{ "sgid", serverGroupId },
				{ "permvalue", permissionValue },
				{ "permnegated", permissionNegated },
				{ "permskip", permissionSkip },
				Ts3PermissionHelper.GetAsParameter(Deserializer.PermissionTransform, permission),
			});

		/// <summary>Adds a set of specified permissions to the server group specified with <paramref name="serverGroupId"/>.
		/// Multiple permissions can be added by providing the four parameters of each permission.</summary>
		public CmdR ServerGroupAddPerm(ServerGroupIdT serverGroupId, Ts3Permission[] permission, int[] permissionValue,
				bool[] permissionNegated, bool[] permissionSkip)
			=> SendCommand<ResponseVoid>(new Ts3Command("servergroupaddperm") {
				{ "sgid", serverGroupId },
				{ "permvalue", permissionValue },
				{ "permnegated", permissionNegated },
				{ "permskip", permissionSkip },
				Ts3PermissionHelper.GetAsMultiParameter(Deserializer.PermissionTransform, permission),
			});

		/// <summary>Adds a client to the server group specified with <paramref name="serverGroupId"/>. Please note that a
		/// client cannot be added to default groups or template groups.</summary>
		public CmdR ServerGroupAddClient(ServerGroupIdT serverGroupId, ClientDbIdT clientDbId)
			=> SendCommand<ResponseVoid>(new Ts3Command("servergroupaddclient") {
				{ "sgid", serverGroupId },
				{ "cldbid", clientDbId },
			});

		/// <summary>Removes a client specified with cldbid from the server group specified with <paramref name="serverGroupId"/>.</summary>
		public CmdR ServerGroupDelClient(ServerGroupIdT serverGroupId, ClientDbIdT clientDbId)
			=> SendCommand<ResponseVoid>(new Ts3Command("servergroupdelclient") {
				{ "sgid", serverGroupId },
				{ "cldbid", clientDbId },
			});

		public CmdR FileTransferStop(ushort serverTransferId, bool delete)
			=> SendCommand<ResponseVoid>(new Ts3Command("ftstop") {
				{ "serverftfid", serverTransferId },
				{ "delete", delete },
			});

		public CmdR FileTransferDeleteFile(ChannelIdT channelId, string[] path, string channelPassword = "")
			=> SendCommand<ResponseVoid>(new Ts3Command("ftdeletefile") {
				{ "cid", channelId },
				{ "cpw", channelPassword },
				{ "name", path },
			});

		public CmdR FileTransferCreateDirectory(ChannelIdT channelId, string path, string channelPassword = "")
			=> SendCommand<ResponseVoid>(new Ts3Command("ftcreatedir") {
				{ "cid", channelId },
				{ "dirname", path },
				{ "cpw", channelPassword },
			});

		public CmdR FileTransferRenameFile(ChannelIdT channelId, string oldName, string channelPassword, string newName,
				ChannelIdT? targetChannel = null, string targetChannelPassword = "")
			=> SendCommand<ResponseVoid>(new Ts3Command("ftrenamefile") {
				{ "cid", channelId },
				{ "oldname", oldName },
				{ "newname", newName },
				{ "cpw", channelPassword },
				{ "tcid", targetChannel },
				{ "tcpw", targetChannel.HasValue ? targetChannelPassword : null },
			});

		public CmdR UploadAvatar(System.IO.Stream image)
		{
			var token = FileTransferManager.UploadFile(image, 0, "/avatar", overwrite: true, createMd5: true);
			if (!token.Ok)
				return token.Error;
			token.Value.Wait();
			if (token.Value.Status != TransferStatus.Done)
				return Util.CustomError("Avatar upload failed");
			var md5 = string.Concat(token.Value.Md5Sum.Select(x => x.ToString("x2")));
			return SendCommand<ResponseVoid>(new Ts3Command("clientupdate") { { "client_flag_avatar", md5 } });
		}

		/// <summary>Deletes the avatar of a user.
		/// Can be called without uid to delete own avatar.</summary>
		/// <param name="clientUid">The client uid where the avatar should be deleted.</param>
		public CmdR DeleteAvatar(string clientUid = null)
		{
			string path = "/avatar_" + clientUid;
			return FileTransferDeleteFile(0, new[] { path });
		}

		public CmdR ClientMove(ClientIdT clientId, ChannelIdT channelId, string channelPassword = null)
			=> SendCommand<ResponseVoid>(new Ts3Command("clientmove") {
				{ "clid", clientId },
				{ "cid", channelId },
				{ "cpw", ClientType == ClientType.Full && channelPassword != null ? Full.Ts3Crypt.HashPassword(channelPassword) : channelPassword },
			});

		// Base Stuff for splitted up commands
		// Some commands behave differently on query and full client

		/// <summary>Creates a new server group using the name specified with <paramref name="name"/> and return its ID.
		/// The optional <paramref name="type"/> parameter can be used to create ServerQuery groups and template groups.</summary>
		public abstract R<ServerGroupAddResponse, CommandError> ServerGroupAdd(string name, GroupType? type = null);

		/// <summary>Displays all server groups the client specified with <paramref name="clDbId"/> is currently residing in.</summary>
		public abstract R<ServerGroupsByClientId[], CommandError> ServerGroupsByClientDbId(ClientDbIdT clDbId);

		public abstract R<FileUpload, CommandError> FileTransferInitUpload(ChannelIdT channelId, string path, string channelPassword,
			ushort clientTransferId, long fileSize, bool overwrite, bool resume);

		public abstract R<FileDownload, CommandError> FileTransferInitDownload(ChannelIdT channelId, string path, string channelPassword,
			ushort clientTransferId, long seek);

		public abstract R<FileTransfer[], CommandError> FileTransferList();

		public abstract R<FileList[], CommandError> FileTransferGetFileList(ChannelIdT channelId, string path, string channelPassword = "");

		public abstract R<FileInfo[], CommandError> FileTransferGetFileInfo(ChannelIdT channelId, string[] path, string channelPassword = "");

		public abstract R<ClientDbIdFromUid, CommandError> ClientGetDbIdFromUid(Uid clientUid);

		public abstract R<ClientIds[], CommandError> GetClientIds(Uid clientUid);

		public abstract R<PermOverview[], CommandError> PermOverview(ClientDbIdT clientDbId, ChannelIdT channelId, params Ts3Permission[] permission);

		public abstract R<PermList[], CommandError> PermissionList();

		public abstract R<ServerConnectionInfo, CommandError> GetServerConnectionInfo();
		#endregion
	}
}
