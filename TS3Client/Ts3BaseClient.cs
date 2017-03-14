namespace TS3Client
{
	using Commands;
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;

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
		public IEnumerable<ResponseDictionary> Send(string command, params CommandParameter[] parameter)
			=> SendCommand<ResponseDictionary>(new Ts3Command(command, parameter.ToList()));

		[DebuggerStepThrough]
		public IEnumerable<ResponseDictionary> Send(string command, CommandParameter[] parameter, params CommandOption[] options)
			=> SendCommand<ResponseDictionary>(new Ts3Command(command, parameter.ToList(), options.ToList()));

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command) where T : IResponse, new()
			=> SendCommand<T>(new Ts3Command(command));

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command, params CommandParameter[] parameter) where T : IResponse, new()
			=> Send<T>(command, parameter.ToList());

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command, List<CommandParameter> parameter) where T : IResponse, new()
			=> SendCommand<T>(new Ts3Command(command, parameter));

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command, CommandParameter[] parameter, params CommandOption[] options) where T : IResponse, new()
			=> SendCommand<T>(new Ts3Command(command, parameter.ToList(), options.ToList()));

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command, List<CommandParameter> parameter, params CommandOption[] options) where T : IResponse, new()
			=> SendCommand<T>(new Ts3Command(command, parameter.ToList(), options.ToList()));

		[DebuggerStepThrough]
		protected void SendNoResponsed(Ts3Command command)
		{
			command.ExpectResponse = false;
			SendCommand<ResponseVoid>(command);
		}

		protected abstract IEnumerable<T> SendCommand<T>(Ts3Command com) where T : IResponse, new();

		#endregion

		#region UNIVERSAL COMMANDS

		public void ChangeName(string newName)
			=> Send("clientupdate",
			new CommandParameter("client_nickname", newName));
		public void ChangeDescription(string newDescription, ClientData client)
			=> Send("clientdbedit",
			new CommandParameter("cldbid", client.DatabaseId),
			new CommandParameter("client_description", newDescription));
		public WhoAmI WhoAmI() // Q ?
			=> Send<WhoAmI>("whoami").FirstOrDefault();
		public void SendMessage(string message, ClientData client)
			=> SendMessage(MessageTarget.Private, client.ClientId, message);
		public void SendMessage(string message, ChannelData channel)
			=> SendMessage(MessageTarget.Channel, (ulong)channel.Id, message);
		public void SendMessage(string message, ServerData server)
			=> SendMessage(MessageTarget.Server, server.VirtualServerId, message);
		public void SendMessage(MessageTarget target, ulong id, string message)
			=> Send("sendtextmessage",
			new CommandParameter("targetmode", (ulong)target),
			new CommandParameter("target", id),
			new CommandParameter("msg", message));
		public void SendGlobalMessage(string message)
			=> Send("gm",
			new CommandParameter("msg", message));
		public void KickClientFromServer(ushort[] clientIds)
			=> KickClient(clientIds, RequestTarget.Server);
		public void KickClientFromChannel(ushort[] clientIds)
			=> KickClient(clientIds, RequestTarget.Channel);
		public void KickClient(ushort[] clientIds, RequestTarget target)
			=> Send("clientkick",
			new CommandParameter("reasonid", (int)target),
			CommandBinder.NewBind("clid", clientIds));
		public IEnumerable<ClientData> ClientList()
			=> ClientList(0);
		public IEnumerable<ClientData> ClientList(ClientListOptions options) => Send<ClientData>("clientlist",
			Ts3Command.NoParameter, options);
		public IEnumerable<ClientServerGroup> ServerGroupsOfClientDbId(ClientData client)
			=> ServerGroupsOfClientDbId(client.DatabaseId);
		public IEnumerable<ClientServerGroup> ServerGroupsOfClientDbId(ulong clDbId)
			=> Send<ClientServerGroup>("servergroupsbyclientid", new CommandParameter("cldbid", clDbId));
		public ClientDbData ClientDbInfo(ClientData client)
			=> ClientDbInfo(client.DatabaseId);
		public ClientDbData ClientDbInfo(ulong clDbId)
			=> Send<ClientDbData>("clientdbinfo", new CommandParameter("cldbid", clDbId)).FirstOrDefault();
		public ClientInfo ClientInfo(ushort clientId)
			=> Send<ClientInfo>("clientinfo", new CommandParameter("clid", clientId)).FirstOrDefault();

		#endregion
	}
}
