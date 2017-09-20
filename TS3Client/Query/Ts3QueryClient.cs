// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Query
{
	using Commands;
	using Messages;
	using System;
	using System.Linq;
	using System.Collections.Generic;
	using System.IO;
	using System.Net.Sockets;

	using ClientUidT = System.String;
	using ClientDbIdT = System.UInt64;
	using ClientIdT = System.UInt16;
	using ChannelIdT = System.UInt64;
	using ServerGroupIdT = System.UInt64;
	using ChannelGroupIdT = System.UInt64;

	public sealed class Ts3QueryClient : Ts3BaseFunctions
	{
		private readonly object sendQueueLock = new object();
		private readonly TcpClient tcpClient;
		private NetworkStream tcpStream;
		private StreamReader tcpReader;
		private StreamWriter tcpWriter;
		private readonly MessageProcessor msgProc;
		private readonly IEventDispatcher dispatcher;

		public override ClientType ClientType => ClientType.Query;
		public override bool Connected => tcpClient.Connected;

		public override event NotifyEventHandler<TextMessage> OnTextMessageReceived;
		public override event NotifyEventHandler<ClientEnterView> OnClientEnterView;
		public override event NotifyEventHandler<ClientLeftView> OnClientLeftView;
		public override event EventHandler<EventArgs> OnConnected;
		public override event EventHandler<DisconnectEventArgs> OnDisconnected;

		public Ts3QueryClient(EventDispatchType dispatcherType)
		{
			tcpClient = new TcpClient();
			msgProc = new MessageProcessor(true);
			dispatcher = EventDispatcherHelper.Create(dispatcherType);
		}

		public override void Connect(ConnectionData conData)
		{
			if (!TsDnsResolver.TryResolve(conData.Address, out remoteAddress))
				throw new Ts3Exception("Could not read or resolve address.");

			try { tcpClient.Connect(remoteAddress); }
			catch (SocketException ex) { throw new Ts3Exception("Could not connect.", ex); }
			ConnectionData = conData;

			tcpStream = tcpClient.GetStream();
			tcpReader = new StreamReader(tcpStream, Util.Encoder);
			tcpWriter = new StreamWriter(tcpStream, Util.Encoder) { NewLine = "\n" };

			for (int i = 0; i < 3; i++)
				tcpReader.ReadLine();

			dispatcher.Init(NetworkLoop, InvokeEvent);
			OnConnected?.Invoke(this, new EventArgs());
			dispatcher.EnterEventLoop();
		}

		public override void Disconnect()
		{
			lock (sendQueueLock)
			{
				SendRaw("quit");
				if (tcpClient.Connected)
					((IDisposable)tcpClient)?.Dispose();
			}
		}

		private void NetworkLoop()
		{
			while (true)
			{
				string line;
				try { line = tcpReader.ReadLine(); }
				catch (IOException) { line = null; }
				if (line == null) break;
				if (string.IsNullOrWhiteSpace(line)) continue;

				var message = line.Trim();
				msgProc.PushMessage(message);
			}
			OnDisconnected?.Invoke(this, new DisconnectEventArgs(MoveReason.LeftServer)); // TODO ??
		}

		private void InvokeEvent(LazyNotification lazyNotification)
		{
			var notification = lazyNotification.Notifications;
			switch (lazyNotification.NotifyType)
			{
			case NotificationType.ChannelCreated: break;
			case NotificationType.ChannelDeleted: break;
			case NotificationType.ChannelChanged: break;
			case NotificationType.ChannelEdited: break;
			case NotificationType.ChannelMoved: break;
			case NotificationType.ChannelPasswordChanged: break;
			case NotificationType.ClientEnterView: OnClientEnterView?.Invoke(this, notification.Cast<ClientEnterView>()); break;
			case NotificationType.ClientLeftView: OnClientLeftView?.Invoke(this, notification.Cast<ClientLeftView>()); break;
			case NotificationType.ClientMoved: break;
			case NotificationType.ServerEdited: break;
			case NotificationType.TextMessage: OnTextMessageReceived?.Invoke(this, notification.Cast<TextMessage>()); break;
			case NotificationType.TokenUsed: break;
			// special
			case NotificationType.Error: break;
			case NotificationType.Unknown:
			default: throw Util.UnhandledDefault(lazyNotification.NotifyType);
			}
		}

		public override IEnumerable<T> SendCommand<T>(Ts3Command com) // Synchronous
		{
			using (var wb = new WaitBlock())
			{
				lock (sendQueueLock)
				{
					msgProc.EnqueueRequest(wb);
					SendRaw(com.ToString());
				}

				return wb.WaitForMessage<T>();
			}
		}

		private void SendRaw(string data)
		{
			if (!tcpClient.Connected)
				return;
			tcpWriter.WriteLine(data);
			tcpWriter.Flush();
		}

		#region QUERY SPECIFIC COMMANDS

		private static readonly string[] TargetTypeString = { "textprivate", "textchannel", "textserver", "channel", "server" };

		public void RegisterNotification(TextMessageTargetMode target, ChannelIdT channel)
			=> RegisterNotification(TargetTypeString[(int)target], channel);

		public void RegisterNotification(ReasonIdentifier target, ChannelIdT channel)
			=> RegisterNotification(TargetTypeString[(int)target], channel);

		private void RegisterNotification(string target, ChannelIdT channel)
		{
			var ev = new CommandParameter("event", target.ToLowerInvariant());
			if (target == "channel")
				Send("servernotifyregister", ev, new CommandParameter("id", channel));
			else
				Send("servernotifyregister", ev);
		}

		public void Login(string username, string password)
			=> Send("login",
			new CommandParameter("client_login_name", username),
			new CommandParameter("client_login_password", password));

		public void UseServer(int serverId)
			=> Send("use",
			new CommandParameter("sid", serverId));

		// Splitted base commands

		public override ServerGroupAddResponse ServerGroupAdd(string name, PermissionGroupDatabaseType? type = null)
			=> Send<ServerGroupAddResponse>("servergroupadd",
				type.HasValue
				? new List<ICommandPart> { new CommandParameter("name", name), new CommandParameter("type", (int)type.Value) }
				: new List<ICommandPart> { new CommandParameter("name", name) }).FirstOrDefault();

		public override IEnumerable<ClientServerGroup> ServerGroupsByClientDbId(ulong clDbId)
			=> Send<ClientServerGroup>("servergroupsbyclientid",
			new CommandParameter("cldbid", clDbId));

		public override FileUpload FileTransferInitUpload(ChannelIdT channelId, string path, string channelPassword,
			ushort clientTransferId, long fileSize, bool overwrite, bool resume)
			=> Send<FileUpload>("ftinitupload",
			new CommandParameter("cid", channelId),
			new CommandParameter("name", path),
			new CommandParameter("cpw", channelPassword),
			new CommandParameter("clientftfid", clientTransferId),
			new CommandParameter("size", fileSize),
			new CommandParameter("overwrite", overwrite),
			new CommandParameter("resume", resume)).First();

		public override FileDownload FileTransferInitDownload(ChannelIdT channelId, string path, string channelPassword,
			ushort clientTransferId, long seek)
			=> Send<FileDownload>("ftinitdownload",
			new CommandParameter("cid", channelId),
			new CommandParameter("name", path),
			new CommandParameter("cpw", channelPassword),
			new CommandParameter("clientftfid", clientTransferId),
			new CommandParameter("seekpos", seek)).First();

		public override IEnumerable<FileTransfer> FileTransferList()
			=> Send<FileTransfer>("ftlist");

		public override IEnumerable<FileList> FileTransferGetFileList(ChannelIdT channelId, string path, string channelPassword = "")
			=> Send<FileList>("ftgetfilelist",
			new CommandParameter("cid", channelId),
			new CommandParameter("path", path),
			new CommandParameter("cpw", channelPassword));

		public override IEnumerable<FileInfoTs> FileTransferGetFileInfo(ChannelIdT channelId, string[] path, string channelPassword = "")
			=> Send<FileInfoTs>("ftgetfileinfo",
			new CommandParameter("cid", channelId),
			new CommandParameter("cpw", channelPassword),
			new CommandMultiParameter("name", path));

		#endregion

		public override void Dispose()
		{
			lock (sendQueueLock)
			{
				tcpWriter?.Dispose();
				tcpWriter = null;

				tcpReader?.Dispose();
				tcpReader = null;

				msgProc.DropQueue();
				dispatcher.Dispose();
			}
		}
	}
}
