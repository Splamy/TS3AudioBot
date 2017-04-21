namespace TS3Client
{
	using Commands;
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;

	using ClientUidT = System.String;
	using ClientDbIdT = System.UInt64;
	using ClientIdT = System.UInt16;
	using ChannelIdT = System.UInt64;
	using ServerGroupIdT = System.UInt64;
	using ChannelGroupIdT = System.UInt64;

	public delegate void NotifyEventHandler<in TEventArgs>(object sender, IEnumerable<TEventArgs> e) where TEventArgs : INotification;

	public abstract class Ts3BaseFunctions : IDisposable
	{
		public abstract event NotifyEventHandler<TextMessage> OnTextMessageReceived;
		public abstract event NotifyEventHandler<ClientEnterView> OnClientEnterView;
		public abstract event NotifyEventHandler<ClientLeftView> OnClientLeftView;
		public abstract event EventHandler<EventArgs> OnConnected;
		public abstract event EventHandler<DisconnectEventArgs> OnDisconnected;

		public abstract bool Connected { get; }
		public abstract ClientType ClientType { get; }

		public abstract void Connect(ConnectionData conData);
		public abstract void Disconnect();
		public abstract void Dispose();

		#region NETWORK SEND

		[DebuggerStepThrough]
		public IEnumerable<ResponseDictionary> Send(string command)
			=> SendCommand<ResponseDictionary>(new Ts3Command(command));

		[DebuggerStepThrough]
		public IEnumerable<ResponseDictionary> Send(string command, params ICommandPart[] parameter)
			=> SendCommand<ResponseDictionary>(new Ts3Command(command, parameter.ToList()));

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command) where T : IResponse, new()
			=> SendCommand<T>(new Ts3Command(command));

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command, params ICommandPart[] parameter) where T : IResponse, new()
			=> Send<T>(command, parameter.ToList());

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command, List<ICommandPart> parameter) where T : IResponse, new()
			=> SendCommand<T>(new Ts3Command(command, parameter));

		[DebuggerStepThrough]
		protected void SendNoResponsed(Ts3Command command)
			=> SendCommand<ResponseVoid>(command.ExpectsResponse(false));

		public abstract IEnumerable<T> SendCommand<T>(Ts3Command com) where T : IResponse, new();

		#endregion

		#region UNIVERSAL COMMANDS

		public void ChangeName(string newName)
			=> Send("clientupdate",
			new CommandParameter("client_nickname", newName));

		public void ChangeDescription(string newDescription, ClientData client)
			=> Send("clientdbedit",
			new CommandParameter("cldbid", client.DatabaseId),
			new CommandParameter("client_description", newDescription));

		/// <summary>Displays information about your current ServerQuery connection including your loginname, etc.</summary>
		public WhoAmI WhoAmI() // Q ?
			=> Send<WhoAmI>("whoami").FirstOrDefault();

		public void SendMessage(string message, ClientData client)
			=> SendMessage(TextMessageTargetMode.Private, client.ClientId, message);

		public void SendMessage(string message, ChannelData channel)
			=> SendMessage(TextMessageTargetMode.Channel, channel.Id, message);

		public void SendMessage(string message, ServerData server)
			=> SendMessage(TextMessageTargetMode.Server, server.VirtualServerId, message);

		/// <summary>Sends a text message to a specified target.
		/// If targetmode is set to <see cref="TextMessageTargetMode.Private"/>, a message is sent to the client with the ID specified by target.
		/// If targetmode is set to <see cref="TextMessageTargetMode.Channel"/> or <see cref="TextMessageTargetMode.Server"/>,
		/// the target parameter will be ignored and a message is sent to the current channel or server respectively.</summary>
		public void SendMessage(TextMessageTargetMode target, ulong id, string message)
			=> Send("sendtextmessage",
			new CommandParameter("targetmode", (int)target),
			new CommandParameter("target", id),
			new CommandParameter("msg", message));

		/// <summary>Sends a text message to all clients on all virtual servers in the TeamSpeak 3 Server instance.</summary>
		public void SendGlobalMessage(string message)
			=> Send("gm",
			new CommandParameter("msg", message));

		public void KickClientFromServer(ClientIdT[] clientIds)
			=> KickClient(clientIds, ReasonIdentifier.Server);

		public void KickClientFromChannel(ClientIdT[] clientIds)
			=> KickClient(clientIds, ReasonIdentifier.Channel);

		/// <summary>Kicks one or more clients specified with clid from their currently joined channel or from the server, depending on <paramref name="reasonId"/>.
		/// The reasonmsg parameter specifies a text message sent to the kicked clients.
		/// This parameter is optional and may only have a maximum of 40 characters.</summary>
		public void KickClient(ClientIdT[] clientIds, ReasonIdentifier reasonId, string reasonMsg = null)
			=> Send("clientkick",
			new CommandParameter("reasonid", (int)reasonId),
			new CommandMultiParameter("clid", clientIds));

		/// <summary>Displays a list of clients online on a virtual server including their ID, nickname, status flags, etc.
		/// The output can be modified using several command options.
		/// Please note that the output will only contain clients which are currently in channels you're able to subscribe to.</summary>
		public IEnumerable<ClientData> ClientList(ClientListOptions options = 0)
			=> Send<ClientData>("clientlist",
			new CommandOption(options));

		/// <summary>Displays detailed database information about a client including unique ID, creation date, etc.</summary>
		public ClientDbData ClientDbInfo(ClientDbIdT clDbId)
			=> Send<ClientDbData>("clientdbinfo",
			new CommandParameter("cldbid", clDbId)).FirstOrDefault();

		/// <summary>Displays detailed configuration information about a client including unique ID, nickname, client version, etc.</summary>
		public ClientInfo ClientInfo(ClientIdT clientId)
			=> Send<ClientInfo>("clientinfo",
			new CommandParameter("clid", clientId)).FirstOrDefault();

		/// <summary>Use a token key gain access to a server or channel group.
		/// Please note that the server will automatically delete the token after it has been used.</summary>
		public void PrivilegeKeyUse(string key)
			=> Send("privilegekeyuse",
			new CommandParameter("token", key));

		/// <summary>Adds a set of specified permissions to the server group specified with <paramref name="serverGroupId"/>.
		/// Multiple permissions can be added by providing the four parameters of each permission.</summary>
		public void ServerGroupAddPerm(ServerGroupIdT serverGroupId, PermissionId permissionId, int permissionValue,
				bool permissionNegated, bool permissionSkip)
			=> Send("servergroupaddperm",
			new CommandParameter("sgid", serverGroupId),
			new CommandParameter("permid", (int)permissionId),
			new CommandParameter("permvalue", permissionValue),
			new CommandParameter("permnegated", permissionNegated),
			new CommandParameter("permskip", permissionSkip));

		/// <summary>Adds a set of specified permissions to the server group specified with <paramref name="serverGroupId"/>.
		/// Multiple permissions can be added by providing the four parameters of each permission.</summary>
		public void ServerGroupAddPerm(ServerGroupIdT serverGroupId, PermissionId[] permissionId, int[] permissionValue,
				bool[] permissionNegated, bool[] permissionSkip)
			=> Send("servergroupaddperm",
			new CommandParameter("sgid", serverGroupId),
			new CommandMultiParameter("permid", permissionId.Cast<int>()),
			new CommandMultiParameter("permvalue", permissionValue),
			new CommandMultiParameter("permnegated", permissionNegated),
			new CommandMultiParameter("permskip", permissionSkip));

		/// <summary>Adds a client to the server group specified with <paramref name="serverGroupId"/>. Please note that a
		/// client cannot be added to default groups or template groups.</summary>
		public void ServerGroupAddClient(ServerGroupIdT serverGroupId, ClientDbIdT clientDbId)
			=> Send("servergroupaddclient",
			new CommandParameter("sgid", serverGroupId),
			new CommandParameter("cldbid", clientDbId));

		/// <summary>Removes a client specified with cldbid from the server group specified with <paramref name="serverGroupId"/>.</summary>
		public void ServerGroupDelClient(ServerGroupIdT serverGroupId, ClientDbIdT clientDbId)
			=> Send("servergroupdelclient",
			new CommandParameter("sgid", serverGroupId),
			new CommandParameter("cldbid", clientDbId));

		// Base Stuff for splitted up commands
		// Some commands behave differently on query and full client

		/// <summary>Creates a new server group using the name specified with <paramref name="name"/> and return its ID.
		/// The optional <paramref name="type"/> parameter can be used to create ServerQuery groups and template groups.</summary>
		public abstract ServerGroupAddResponse ServerGroupAdd(string name, PermissionGroupDatabaseType? type = null);

		/// <summary>Displays all server groups the client specified with <paramref name="clDbId"/> is currently residing in.</summary>
		public abstract IEnumerable<ClientServerGroup> ServerGroupsByClientDbId(ClientDbIdT clDbId);

		#endregion
	}
}
