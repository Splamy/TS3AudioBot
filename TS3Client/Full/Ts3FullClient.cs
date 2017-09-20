// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Full
{
	using Commands;
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;

	using ClientUidT = System.String;
	using ClientDbIdT = System.UInt64;
	using ClientIdT = System.UInt16;
	using ChannelIdT = System.UInt64;
	using ServerGroupIdT = System.UInt64;
	using ChannelGroupIdT = System.UInt64;

	public sealed class Ts3FullClient : Ts3BaseFunctions
	{
		private readonly Ts3Crypt ts3Crypt;
		private readonly PacketHandler packetHandler;
		private readonly MessageProcessor msgProc;

		private readonly object statusLock = new object();

		private int returnCode;
		private bool wasExit;

		private readonly IEventDispatcher dispatcher;
		public override ClientType ClientType => ClientType.Full;
		public ushort ClientId => packetHandler.ClientId;
		public string QuitMessage { get; set; } = "Disconnected";
		public VersionSign VersionSign { get; private set; }
		private Ts3ClientStatus status;
		public override bool Connected { get { lock (statusLock) return status == Ts3ClientStatus.Connected; } }
		private ConnectionDataFull connectionDataFull;

		public override event NotifyEventHandler<TextMessage> OnTextMessageReceived;
		public override event NotifyEventHandler<ClientEnterView> OnClientEnterView;
		public override event NotifyEventHandler<ClientLeftView> OnClientLeftView;
		public event NotifyEventHandler<ClientMoved> OnClientMoved;
		public override event EventHandler<EventArgs> OnConnected;
		public override event EventHandler<DisconnectEventArgs> OnDisconnected;
		public event EventHandler<CommandError> OnErrorEvent;

		public Ts3FullClient(EventDispatchType dispatcherType)
		{
			status = Ts3ClientStatus.Disconnected;
			ts3Crypt = new Ts3Crypt();
			packetHandler = new PacketHandler(ts3Crypt);
			msgProc = new MessageProcessor(false);
			dispatcher = EventDispatcherHelper.Create(dispatcherType);
			wasExit = true;
		}

		public override void Connect(ConnectionData conData)
		{
			if (!(conData is ConnectionDataFull conDataFull)) throw new ArgumentException($"Use the {nameof(ConnectionDataFull)} deriverate to connect with the full client.", nameof(conData));
			if (conDataFull.Identity == null) throw new ArgumentNullException(nameof(conDataFull.Identity));
			if (conDataFull.VersionSign == null) throw new ArgumentNullException(nameof(conDataFull.VersionSign));
			connectionDataFull = conDataFull;
			ConnectionData = conData;

			Disconnect();

			if (!TsDnsResolver.TryResolve(conData.Address, out remoteAddress))
				throw new Ts3Exception("Could not read or resolve address.");

			lock (statusLock)
			{
				returnCode = 0;
				wasExit = false;

				VersionSign = conDataFull.VersionSign;
				ts3Crypt.Identity = conDataFull.Identity;

				packetHandler.Connect(remoteAddress);
				dispatcher.Init(NetworkLoop, InvokeEvent);
			}
			dispatcher.EnterEventLoop();
		}

		public override void Disconnect()
		{
			DisconnectInternal();
			while (true)
			{
				if (wasExit)
					break;
				dispatcher.DoWork();
				if (!wasExit)
					Thread.Sleep(1);
			}
		}

		private void DisconnectInternal(bool triggerEvent = true)
		{
			lock (statusLock)
			{
				if (wasExit)
					return;

				switch (status)
				{
				case Ts3ClientStatus.Disconnected:
					wasExit = true;
					packetHandler.Stop();
					msgProc.DropQueue();
					dispatcher.Dispose();
					if (triggerEvent)
						OnDisconnected?.Invoke(this, new DisconnectEventArgs(packetHandler.ExitReason ?? MoveReason.LeftServer));
					break;
				case Ts3ClientStatus.Disconnecting:
					break;
				case Ts3ClientStatus.Connected:
					ClientDisconnect(MoveReason.LeftServer, QuitMessage);
					status = Ts3ClientStatus.Disconnecting;
					break;
				case Ts3ClientStatus.Connecting:
					break;
				default:
					throw Util.UnhandledDefault(status);
				}
			}
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
			case NotificationType.ClientLeftView:
				var clientLeftArr = notification.Cast<ClientLeftView>().ToArray();
				var leftViewEvent = clientLeftArr.FirstOrDefault(clv => clv.ClientId == packetHandler.ClientId);
				if (leftViewEvent != null)
				{
					packetHandler.ExitReason = leftViewEvent.Reason;
					lock (statusLock)
					{
						status = Ts3ClientStatus.Disconnected;
						DisconnectInternal();
					}
					break;
				}
				OnClientLeftView?.Invoke(this, clientLeftArr);
				break;

			case NotificationType.ClientMoved: OnClientMoved?.Invoke(this, notification.Cast<ClientMoved>()); break;
			case NotificationType.ServerEdited: break;
			case NotificationType.TextMessage: OnTextMessageReceived?.Invoke(this, notification.Cast<TextMessage>()); break;
			case NotificationType.TokenUsed: break;
			// full client events
			case NotificationType.InitIvExpand: ProcessInitIvExpand((InitIvExpand)notification.FirstOrDefault()); break;
			case NotificationType.InitServer: ProcessInitServer((InitServer)notification.FirstOrDefault()); break;
			case NotificationType.ChannelList: break;
			case NotificationType.ChannelListFinished: ChannelSubscribeAll(); break;
			case NotificationType.ClientNeededPermissions: break;
			case NotificationType.ClientChannelGroupChanged: break;
			case NotificationType.ClientServerGroupAdded: break;
			case NotificationType.ConnectionInfoRequest: ProcessConnectionInfoRequest(); break;
			case NotificationType.ChannelSubscribed: break;
			case NotificationType.ChannelUnsubscribed: break;
			case NotificationType.ClientChatComposing: break;
			case NotificationType.ServerGroupList: break;
			case NotificationType.ServerGroupsByClientId: break;
			case NotificationType.StartUpload: break;
			case NotificationType.StartDownload: break;
			case NotificationType.FileTransfer: break;
			case NotificationType.FileTransferStatus: break;
			case NotificationType.FileList: break;
			case NotificationType.FileListFinished: break;
			case NotificationType.FileInfo: break;
			// special
			case NotificationType.Error:
				lock (statusLock)
				{
					if (status == Ts3ClientStatus.Connecting)
					{
						status = Ts3ClientStatus.Disconnected;
						DisconnectInternal(false);
					}
				}

				OnErrorEvent?.Invoke(this, (CommandError)notification.First());
				break;
			case NotificationType.Unknown:
			default: throw Util.UnhandledDefault(lazyNotification.NotifyType);
			}
		}

		private void NetworkLoop()
		{
			while (true)
			{
				lock (statusLock)
				{
					if (wasExit)
						break;
				}
				if (wasExit)
					break;

				IncomingPacket packet = packetHandler.FetchPacket();
				if (packet == null)
					break;

				switch (packet.PacketType)
				{
				case PacketType.Command:
				case PacketType.CommandLow:
					string message = Util.Encoder.GetString(packet.Data, 0, packet.Data.Length);
					var result = msgProc.PushMessage(message);
					if (result.HasValue)
						dispatcher.Invoke(result.Value);
					break;

				case PacketType.Voice:
				case PacketType.VoiceWhisper:
					// VOICE

					break;

				case PacketType.Init1:
					var forwardData = ts3Crypt.ProcessInit1(packet.Data);
					if (forwardData == null)
						break;
					packetHandler.AddOutgoingPacket(forwardData, PacketType.Init1);
					break;
				}
			}

			lock (statusLock)
			{
				status = Ts3ClientStatus.Disconnected;
				DisconnectInternal();
			}
		}

		private void ProcessInitIvExpand(InitIvExpand initIvExpand)
		{
			var password = connectionDataFull.IsPasswordHashed
				? connectionDataFull.Password
				: Ts3Crypt.HashPassword(connectionDataFull.Password);

			ts3Crypt.CryptoInit(initIvExpand.Alpha, initIvExpand.Beta, initIvExpand.Omega);
			packetHandler.CryptoInitDone();
			ClientInit(
				connectionDataFull.Username,
				true, true,
				connectionDataFull.DefaultChannel, string.Empty, password,
				string.Empty, string.Empty, string.Empty, "123,456",
				VersionSign);
		}

		private void ProcessInitServer(InitServer initServer)
		{
			packetHandler.ClientId = initServer.ClientId;
			packetHandler.ReceiveInitAck();

			lock (statusLock)
				status = Ts3ClientStatus.Connected;
			OnConnected?.Invoke(this, new EventArgs());
		}

		private void ProcessConnectionInfoRequest()
		{
			SendNoResponsed(packetHandler.NetworkStats.GenerateStatusAnswer());
		}

		public override IEnumerable<T> SendCommand<T>(Ts3Command com)
		{
			using (var wb = new WaitBlock())
			{
				SendCommandBase(wb, com);
				if (com.ExpectResponse)
					return wb.WaitForMessage<T>();
				else
					return null;
			}
		}

		private LazyNotification SendSpecialCommand(Ts3Command com, params NotificationType[] dependsOn)
		{
			if (!com.ExpectResponse)
				throw new ArgumentException("A special command must take a response");

			using (var wb = new WaitBlock(dependsOn))
			{
				SendCommandBase(wb, com);
				return wb.WaitForNotification();
			}
		}

		private void SendCommandBase(WaitBlock wb, Ts3Command com)
		{
			if (com.ExpectResponse)
			{
				var responseNumber = Interlocked.Increment(ref returnCode);
				var retCodeParameter = new CommandParameter("return_code", responseNumber);
				com.AppendParameter(retCodeParameter);
				msgProc.EnqueueRequest(retCodeParameter.Value, wb);
			}

			byte[] data = Util.Encoder.GetBytes(com.ToString());
			lock (statusLock)
			{
				if (wasExit)
					throw new Ts3CommandException(new CommandError { Id = Ts3ErrorCode.custom_error, Message = "Connection closed" });
				packetHandler.AddOutgoingPacket(data, PacketType.Command);
			}
		}

		public override void Dispose()
		{
			Disconnect();
			dispatcher?.Dispose();
		}

		#region FULLCLIENT SPECIFIC COMMANDS

		public void ChangeIsChannelCommander(bool isChannelCommander)
			=> Send("clientupdate",
			new CommandParameter("client_is_channel_commander", isChannelCommander));

		public void ClientInit(string nickname, bool inputHardware, bool outputHardware,
				string defaultChannel, string defaultChannelPassword, string serverPassword, string metaData,
				string nicknamePhonetic, string defaultToken, string hwid, VersionSign versionSign)
			=> SendNoResponsed(
				new Ts3Command("clientinit", new List<ICommandPart>() {
					new CommandParameter("client_nickname", nickname),
					new CommandParameter("client_version", versionSign.Name),
					new CommandParameter("client_platform", versionSign.PlattformName),
					new CommandParameter("client_input_hardware", inputHardware),
					new CommandParameter("client_output_hardware", outputHardware),
					new CommandParameter("client_default_channel", defaultChannel),
					new CommandParameter("client_default_channel_password", defaultChannelPassword), // base64(sha1(pass))
					new CommandParameter("client_server_password", serverPassword), // base64(sha1(pass))
					new CommandParameter("client_meta_data", metaData),
					new CommandParameter("client_version_sign", versionSign.Sign),
					new CommandParameter("client_key_offset", ts3Crypt.Identity.ValidKeyOffset),
					new CommandParameter("client_nickname_phonetic", nicknamePhonetic),
					new CommandParameter("client_default_token", defaultToken),
					new CommandParameter("hwid", hwid) }));

		public void ClientDisconnect(MoveReason reason, string reasonMsg)
			=> SendNoResponsed(
				new Ts3Command("clientdisconnect", new List<ICommandPart>() {
					new CommandParameter("reasonid", (int)reason),
					new CommandParameter("reasonmsg", reasonMsg) }));

		public void ChannelSubscribeAll()
			=> Send("channelsubscribeall");

		public void ChannelUnsubscribeAll()
			=> Send("channelunsubscribeall");

		public void SendAudio(byte[] buffer, int length, Codec codec)
		{
			// [X,X,Y,DATA]
			// > X is a ushort in H2N order of a own audio packet counter
			//     it seems it can be the same as the packet counter so we will let the packethandler do it.
			// > Y is the codec byte (see Enum)
			byte[] tmpBuffer = new byte[length + 3];
			tmpBuffer[2] = (byte)codec;
			Array.Copy(buffer, 0, tmpBuffer, 3, length);
			buffer = tmpBuffer;

			packetHandler.AddOutgoingPacket(buffer, PacketType.Voice);
		}

		public void SendAudioWhisper(byte[] buffer, int length, Codec codec, IList<ChannelIdT> channelIds, IList<ClientIdT> clientIds)
		{
			// [X,X,Y,N,M,(U,U,U,U,U,U,U,U)*,(T,T)*,DATA]
			// > X is a ushort in H2N order of a own audio packet counter
			//     it seems it can be the same as the packet counter so we will let the packethandler do it.
			// > Y is the codec byte (see Enum)
			// > N is a byte, the count of ChannelIds to send to
			// > M is a byte, the count of ClientIds to send to
			// > U is a ulong in H2N order of each targeted channelId, (U...U) is repeated N times
			// > T is a ushort in H2N order of each targeted clientId, (T...T) is repeated M times
			int offset = 2 + 1 + 2 + channelIds.Count * 8 + clientIds.Count * 2;
			byte[] tmpBuffer = new byte[length + offset];
			tmpBuffer[2] = (byte)codec;
			tmpBuffer[3] = (byte)channelIds.Count;
			tmpBuffer[4] = (byte)clientIds.Count;
			for (int i = 0; i < channelIds.Count; i++)
				NetUtil.H2N(channelIds[i], tmpBuffer, 5 + (i * 8));
			for (int i = 0; i < clientIds.Count; i++)
				NetUtil.H2N(clientIds[i], tmpBuffer, 5 + channelIds.Count * 8 + (i * 2));
			Array.Copy(buffer, 0, tmpBuffer, offset, length);
			buffer = tmpBuffer;

			packetHandler.AddOutgoingPacket(buffer, PacketType.VoiceWhisper);
		}

		public void SendAudioGroupWhisper(byte[] buffer, int length, Codec codec, GroupWhisperType type, GroupWhisperTarget target, ulong targetId = 0)
		{
			// [X,X,Y,N,M,U,U,U,U,U,U,U,U,DATA]
			// > X is a ushort in H2N order of a own audio packet counter
			//     it seems it can be the same as the packet counter so we will let the packethandler do it.
			// > Y is the codec byte (see Enum)
			// > N is a byte, specifying the GroupWhisperType
			// > M is a byte, specifying the GroupWhisperTarget
			// > U is a ulong in H2N order for the targeted channelId or groupId (0 if not applicable)
			byte[] tmpBuffer = new byte[length + 13];
			tmpBuffer[2] = (byte)codec;
			tmpBuffer[3] = (byte)type;
			tmpBuffer[4] = (byte)target;
			NetUtil.H2N(targetId, tmpBuffer, 5);
			Array.Copy(buffer, 0, tmpBuffer, 13, length);
			buffer = tmpBuffer;

			packetHandler.AddOutgoingPacket(buffer, PacketType.VoiceWhisper, PacketFlags.Newprotocol);
		}

		// Splitted base commands

		public override ServerGroupAddResponse ServerGroupAdd(string name, PermissionGroupDatabaseType? type = null)
		{
			var cmd = new Ts3Command("servergroupadd", new List<ICommandPart> { new CommandParameter("name", name) });
			if (type.HasValue)
				cmd.AppendParameter(new CommandParameter("type", (int)type.Value));
			var answer = SendSpecialCommand(cmd, NotificationType.ServerGroupList).Notifications
				.Cast<ServerGroupList>()
				.FirstOrDefault(x => x.Name == name);
			if (answer == null)
				throw new Ts3CommandException(new CommandError() { Id = Ts3ErrorCode.custom_error, Message = "Missing answer" });
			else
				return new ServerGroupAddResponse() { ServerGroupId = answer.ServerGroupId };
		}

		public override IEnumerable<ClientServerGroup> ServerGroupsByClientDbId(ClientDbIdT clDbId)
		{
			return SendSpecialCommand(
				new Ts3Command("servergroupsbyclientid",
					new List<ICommandPart> { new CommandParameter("cldbid", clDbId) }),
				NotificationType.ServerGroupsByClientId)
				.Notifications
				.Cast<ClientServerGroup>()
				.Where(x => x.ClientDbId == clDbId);
		}

		public override FileUpload FileTransferInitUpload(ChannelIdT channelId, string path, string channelPassword, ushort clientTransferId,
			long fileSize, bool overwrite, bool resume)
		{
			var lazyNot = SendSpecialCommand(new Ts3Command("ftinitupload", new List<ICommandPart>() {
			new CommandParameter("cid", channelId),
			new CommandParameter("name", path),
			new CommandParameter("cpw", channelPassword),
			new CommandParameter("clientftfid", clientTransferId),
			new CommandParameter("size", fileSize),
			new CommandParameter("overwrite", overwrite),
			new CommandParameter("resume", resume) }), NotificationType.StartUpload, NotificationType.FileTransferStatus);
			if (lazyNot.NotifyType == NotificationType.StartUpload)
				return lazyNot.Notifications.Cast<FileUpload>().First();
			else
			{
				var ft = lazyNot.Notifications.Cast<FileTransferStatus>().First();
				throw new Ts3CommandException(new CommandError() { Id = ft.Status, Message = ft.Message });
			}
		}

		public override FileDownload FileTransferInitDownload(ChannelIdT channelId, string path, string channelPassword, ushort clientTransferId,
			long seek)
		{
			var lazyNot = SendSpecialCommand(new Ts3Command("ftinitdownload", new List<ICommandPart>() {
			new CommandParameter("cid", channelId),
			new CommandParameter("name", path),
			new CommandParameter("cpw", channelPassword),
			new CommandParameter("clientftfid", clientTransferId),
			new CommandParameter("seekpos", seek) }), NotificationType.StartDownload, NotificationType.FileTransferStatus);
			if (lazyNot.NotifyType == NotificationType.StartDownload)
				return lazyNot.Notifications.Cast<FileDownload>().First();
			else
			{
				var ft = lazyNot.Notifications.Cast<FileTransferStatus>().First();
				throw new Ts3CommandException(new CommandError() { Id = ft.Status, Message = ft.Message });
			}
		}

		public override IEnumerable<FileTransfer> FileTransferList()
			=> SendSpecialCommand(new Ts3Command("ftlist"), NotificationType.FileTransfer)
			.Notifications.Cast<FileTransfer>();

		public override IEnumerable<FileList> FileTransferGetFileList(ChannelIdT channelId, string path, string channelPassword = "")
			=> SendSpecialCommand(new Ts3Command("ftgetfilelist", new List<ICommandPart>() {
			new CommandParameter("cid", channelId),
			new CommandParameter("path", path),
			new CommandParameter("cpw", channelPassword) }), NotificationType.FileList)
			.Notifications
			.Cast<FileList>();

		public override IEnumerable<FileInfoTs> FileTransferGetFileInfo(ChannelIdT channelId, string[] path, string channelPassword = "")
			=> SendSpecialCommand(new Ts3Command("ftgetfileinfo", new List<ICommandPart>() {
			new CommandParameter("cid", channelId),
			new CommandParameter("cpw", channelPassword),
			new CommandMultiParameter("name", path) }), NotificationType.FileInfo)
			.Notifications
			.Cast<FileInfoTs>();

		#endregion

		private enum Ts3ClientStatus
		{
			Disconnected,
			Disconnecting,
			Connected,
			Connecting,
		}
	}
}
