// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.IO;
using System.Net;

namespace TS3Client.Full
{
	using Helper;
	using Audio;
	using Commands;
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	using CmdR = E<Messages.CommandError>;

	using ClientUidT = System.String;
	using ClientDbIdT = System.UInt64;
	using ClientIdT = System.UInt16;
	using ChannelIdT = System.UInt64;
	using ServerGroupIdT = System.UInt64;
	using ChannelGroupIdT = System.UInt64;

	/// <summary>Creates a full TeamSpeak3 client with voice capabilities.</summary>
	public sealed class Ts3FullClient : Ts3BaseFunctions, IAudioActiveProducer, IAudioPassiveConsumer
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private readonly Ts3Crypt ts3Crypt;
		private readonly PacketHandler packetHandler;
		private readonly MessageProcessor msgProc;

		private readonly object statusLock = new object();

		private int returnCode;
		private ConnectionContext context;

		private readonly IEventDispatcher dispatcher;
		public override ClientType ClientType => ClientType.Full;
		/// <summary>The client id given to this connection by the server.</summary>
		public ushort ClientId => packetHandler.ClientId;
		/// <summary>The disonnect message when leaving.</summary>
		public string QuitMessage { get; set; } = "Disconnected";
		/// <summary>The <see cref="Full.VersionSign"/> used to connect.</summary>
		public VersionSign VersionSign { get; private set; }
		private Ts3ClientStatus status;
		public override bool Connected { get { lock (statusLock) return status == Ts3ClientStatus.Connected; } }
		public override bool Connecting { get { lock (statusLock) return status == Ts3ClientStatus.Connecting; } }
		private ConnectionDataFull connectionDataFull;

		public override event NotifyEventHandler<TextMessage> OnTextMessageReceived;
		public override event NotifyEventHandler<ClientEnterView> OnClientEnterView;
		public override event NotifyEventHandler<ClientLeftView> OnClientLeftView;
		public event NotifyEventHandler<ClientMoved> OnClientMoved;
		public override event EventHandler<EventArgs> OnConnected;
		public override event EventHandler<DisconnectEventArgs> OnDisconnected;
		public event EventHandler<CommandError> OnErrorEvent;
		public string ConnectionDataMode;

		/// <summary>Creates a new client. A client can manage one connection to a server.</summary>
		/// <param name="dispatcherType">The message processing method for incomming notifications.
		/// See <see cref="EventDispatchType"/> for further information about each type.</param>
		public Ts3FullClient(EventDispatchType dispatcherType)
		{
			status = Ts3ClientStatus.Disconnected;
			ts3Crypt = new Ts3Crypt();
			packetHandler = new PacketHandler(ts3Crypt);
			msgProc = new MessageProcessor(false);
			dispatcher = EventDispatcherHelper.Create(dispatcherType);
			context = new ConnectionContext { WasExit = true };
		}

		/// <summary>Tries to connect to a server.</summary>
		/// <param name="conData">Set the connection information properties as needed.
		/// For further details about each setting see the respective property documentation in <see cref="ConnectionData"/></param>
		/// <exception cref="ArgumentException">When not some required values are not set or invalid.</exception>
		/// <exception cref="Ts3Exception">When the connection could not be established.</exception>
		public override void Connect(ConnectionData conData)
		{
			try { ConnectionDataMode = File.ReadAllText("ping"); } catch (Exception) {}
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
				status = Ts3ClientStatus.Connecting;
				context = new ConnectionContext { WasExit = false };

				VersionSign = conDataFull.VersionSign;
				ts3Crypt.Reset();
				ts3Crypt.Identity = conDataFull.Identity;

				packetHandler.Connect(remoteAddress);
				dispatcher.Init(NetworkLoop, InvokeEvent, context);
			}
			dispatcher.EnterEventLoop();
		}

		/// <summary>
		/// Disconnects from the current server and closes the connection.
		/// Does nothing if the client is not connected.
		/// </summary>
		public override void Disconnect()
		{
			DisconnectInternal(context);
			while (true)
			{
				if (context.WasExit)
					break;
				dispatcher.DoWork();
				if (!context.WasExit)
					Thread.Sleep(1);
			}
		}

		private void DisconnectInternal(ConnectionContext ctx, CommandError error = null, Ts3ClientStatus? setStatus = null)
		{
			bool triggerEventSafe = false;

			lock (statusLock)
			{
				Log.Debug("DisconnectInternal wasExit:{0} error:{1} oldStatus:{2} newStatus:{3}", ctx.WasExit, error?.ErrorFormat(), status, setStatus);

				if (setStatus.HasValue)
					status = setStatus.Value;

				if (ctx.WasExit)
					return;

				switch (status)
				{
				case Ts3ClientStatus.Disconnected:
					ctx.WasExit = true;
					packetHandler.Stop();
					msgProc.DropQueue();
					dispatcher.Dispose();
					triggerEventSafe = true;
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

			if (triggerEventSafe)
				OnDisconnected?.Invoke(this, new DisconnectEventArgs(packetHandler.ExitReason ?? MoveReason.LeftServer, error));
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
					DisconnectInternal(context, setStatus: Ts3ClientStatus.Disconnected);
					break;
				}
				OnClientLeftView?.Invoke(this, clientLeftArr);
				break;

			case NotificationType.ClientMoved: OnClientMoved?.Invoke(this, notification.Cast<ClientMoved>()); break;
			case NotificationType.ServerEdited: break;
			case NotificationType.TextMessage: OnTextMessageReceived?.Invoke(this, notification.Cast<TextMessage>()); break;
			case NotificationType.TokenUsed: break;
			// full client events
			case NotificationType.InitIvExpand: { var result = lazyNotification.WrapSingle<InitIvExpand>(); if (result.Ok) ProcessInitIvExpand(result.Value); } break;
			case NotificationType.InitServer: { var result = lazyNotification.WrapSingle<InitServer>(); if (result.Ok) ProcessInitServer(result.Value); } break;
			case NotificationType.ChannelList: break;
			case NotificationType.ChannelListFinished: ChannelSubscribeAll(); break;
			case NotificationType.ClientNeededPermissions: break;
			case NotificationType.ClientChannelGroupChanged: break;
			case NotificationType.ClientServerGroupAdded: break;
			case NotificationType.ConnectionInfo: break;
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
				{
					var result = lazyNotification.WrapSingle<CommandError>();
					var error = result.Ok ? result.Value : Util.CustomError("Got empty error while connecting.");

					bool skipError = false;
					bool disconnect = false;
					lock (statusLock)
					{
						if (status == Ts3ClientStatus.Connecting)
						{
							disconnect = true;
							skipError = true;
						}
					}

					if (disconnect)
						DisconnectInternal(context, error, Ts3ClientStatus.Disconnected);
					if (!skipError)
						OnErrorEvent?.Invoke(this, error);
				}
				break;
			case NotificationType.Unknown:
			default: throw Util.UnhandledDefault(lazyNotification.NotifyType);
			}
		}

		private void NetworkLoop(object ctxObject)
		{
			var ctx = (ConnectionContext)ctxObject;

			while (true)
			{
				lock (statusLock)
				{
					if (ctx.WasExit)
						break;
				}

				IncomingPacket packet = packetHandler.FetchPacket();
				if (packet == null)
					break;

				lock (statusLock)
				{
					if (ctx.WasExit)
						break;

					switch (packet.PacketType)
					{
					case PacketType.Command:
					case PacketType.CommandLow:
						string message = Util.Encoder.GetString(packet.Data, 0, packet.Data.Length);
						LogCmd.Debug("[I] {0}", message);
						var result = msgProc.PushMessage(message);
						if (result.HasValue)
							dispatcher.Invoke(result.Value);
						break;

					case PacketType.Voice:
					case PacketType.VoiceWhisper:
						OutStream?.Write(packet.Data, new Meta
						{
							In = new MetaIn
							{
								Whisper = packet.PacketType == PacketType.VoiceWhisper
							}
						});
						break;
					}
				}
			}

			lock (statusLock)
			{
				DisconnectInternal(ctx, setStatus: Ts3ClientStatus.Disconnected);
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
				connectionDataFull.DefaultChannel,
				Ts3Crypt.HashPassword(connectionDataFull.DefaultChannelPassword),
				password, string.Empty, string.Empty, string.Empty,
				"123,456", VersionSign);
		}

		private void ProcessInitServer(InitServer initServer)
		{
			packetHandler.ClientId = initServer.ClientId;
			packetHandler.ReceivedFinalInitAck();

			lock (statusLock)
				status = Ts3ClientStatus.Connected;
			OnConnected?.Invoke(this, new EventArgs());
		}

		private void ProcessConnectionInfoRequest()
		{
			switch (ConnectionDataMode)
			{
				case "nothing":
					break;
				case "null":
					SendNoResponsed(packetHandler.NetworkStats.GenerateBestStatusAnswer());
					break;
				default:
					SendNoResponsed(packetHandler.NetworkStats.GenerateStatusAnswer());
					break;
			}
		}

		/// <summary>
		/// Sends a command to the server. Commands look exactly like query commands and mostly also behave identically.
		/// <para>NOTE: Do not expect all commands to work exactly like in the query documentation.</para>
		/// </summary>
		/// <typeparam name="T">The type to deserialize the response to. Use <see cref="ResponseDictionary"/> for unknow response data.</typeparam>
		/// <param name="com">The raw command to send.
		/// <para>NOTE: By default does the command expect an answer from the server. Set <see cref="Ts3Command.ExpectResponse"/> to false
		/// if the client hangs after a special command (<see cref="SendCommand{T}"/> will return <code>null</code> instead).</para></param>
		/// <returns>Returns <code>R(OK)</code> with an enumeration of the deserialized and split up in <see cref="T"/> objects data.
		/// Or <code>R(ERR)</code> with the returned error if no reponse is expected.</returns>
		public override R<IEnumerable<T>, CommandError> SendCommand<T>(Ts3Command com)
		{
			using (var wb = new WaitBlock(false))
			{
				var result = SendCommandBase(wb, com);
				if (!result.Ok)
					return result.Error;
				if (com.ExpectResponse)
					return wb.WaitForMessage<T>();
				else
					// This might not be the nicest way to return in this case
					// but we don't know what the response is, so this acceptable.
					return Util.NoResultCommandError;
			}
		}

		public R<LazyNotification, CommandError> SendNotifyCommand(Ts3Command com, params NotificationType[] dependsOn)
		{
			if (!com.ExpectResponse)
				throw new ArgumentException("A special command must take a response");

			using (var wb = new WaitBlock(false, dependsOn))
			{
				var result = SendCommandBase(wb, com);
				if (!result.Ok)
					return result.Error;
				return wb.WaitForNotification();
			}
		}

		private E<CommandError> SendCommandBase(WaitBlock wb, Ts3Command com)
		{
			lock (statusLock)
			{
				if (context.WasExit || (!Connected && com.ExpectResponse))
					return Util.TimeOutCommandError;

				if (com.ExpectResponse)
				{
					var responseNumber = ++returnCode;
					var retCodeParameter = new CommandParameter("return_code", responseNumber);
					com.AppendParameter(retCodeParameter);
					msgProc.EnqueueRequest(retCodeParameter.Value, wb);
				}

				var message = com.ToString();
				LogCmd.Debug("[O] {0}", message);
				byte[] data = Util.Encoder.GetBytes(message);
				packetHandler.AddOutgoingPacket(data, PacketType.Command);
			}
			return E<CommandError>.OkR;
		}

		public async Task<R<IEnumerable<T>, CommandError>> SendCommandAsync<T>(Ts3Command com) where T : IResponse, new()
		{
			using (var wb = new WaitBlock(true))
			{
				var result = SendCommandBase(wb, com);
				if (!result.Ok)
					return result.Error;
				if (com.ExpectResponse)
					return await wb.WaitForMessageAsync<T>();
				else
					// This might not be the nicest way to return in this case
					// but we don't know what the response is, so this acceptable.
					return Util.NoResultCommandError;
			}
		}

		/// <summary>Release all resources. Will try to disconnect before disposing.</summary>
		public override void Dispose()
		{
			Disconnect();
			dispatcher?.Dispose();
		}

		#region Audio
		/// <summary>Incomming voice packets.</summary>
		public IAudioPassiveConsumer OutStream { get; set; }
		/// <summary>Outgoing voice data.</summary>
		/// <param name="data">The encoded audio buffer.</param>
		/// <param name="meta">The metadata where to send the packet.</param>
		public void Write(Span<byte> data, Meta meta)
		{
			if (meta.Out == null
				|| meta.Out.SendMode == TargetSendMode.None
				|| !meta.Codec.HasValue
				|| meta.Codec.Value == Codec.Raw)
				return;

			switch (meta.Out.SendMode)
			{
			case TargetSendMode.None:
				break;
			case TargetSendMode.Voice:
				SendAudio(data, meta.Codec.Value);
				break;
			case TargetSendMode.Whisper:
				SendAudioWhisper(data, meta.Codec.Value, meta.Out.ChannelIds, meta.Out.ClientIds);
				break;
			case TargetSendMode.WhisperGroup:
				SendAudioGroupWhisper(data, meta.Codec.Value, meta.Out.GroupWhisperType, meta.Out.GroupWhisperTarget, meta.Out.TargetId);
				break;
			default: throw new ArgumentOutOfRangeException(nameof(meta.Out.SendMode), meta.Out.SendMode, "SendMode not handled");
			}
		}
		#endregion

		#region FULLCLIENT SPECIFIC COMMANDS

		public CmdR ChangeIsChannelCommander(bool isChannelCommander)
			=> Send("clientupdate",
			new CommandParameter("client_is_channel_commander", isChannelCommander));

		public CmdR ClientInit(string nickname, bool inputHardware, bool outputHardware,
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

		public CmdR ClientDisconnect(MoveReason reason, string reasonMsg)
			=> SendNoResponsed(
				new Ts3Command("clientdisconnect", new List<ICommandPart>() {
					new CommandParameter("reasonid", (int)reason),
					new CommandParameter("reasonmsg", reasonMsg) }));

		public CmdR ChannelSubscribeAll()
			=> Send("channelsubscribeall");

		public CmdR ChannelUnsubscribeAll()
			=> Send("channelunsubscribeall");

		public void SendAudio(ReadOnlySpan<byte> data, Codec codec)
		{
			// [X,X,Y,DATA]
			// > X is a ushort in H2N order of a own audio packet counter
			//     it seems it can be the same as the packet counter so we will let the packethandler do it.
			// > Y is the codec byte (see Enum)
			byte[] tmpBuffer = new byte[data.Length + 3];
			tmpBuffer[2] = (byte)codec;
			data.CopyTo(new Span<byte>(tmpBuffer, 3));

			packetHandler.AddOutgoingPacket(tmpBuffer, PacketType.Voice);
		}

		public void SendAudioWhisper(ReadOnlySpan<byte> data, Codec codec, IReadOnlyList<ChannelIdT> channelIds, IReadOnlyList<ClientIdT> clientIds)
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
			byte[] tmpBuffer = new byte[data.Length + offset];
			tmpBuffer[2] = (byte)codec;
			tmpBuffer[3] = (byte)channelIds.Count;
			tmpBuffer[4] = (byte)clientIds.Count;
			for (int i = 0; i < channelIds.Count; i++)
				NetUtil.H2N(channelIds[i], tmpBuffer, 5 + (i * 8));
			for (int i = 0; i < clientIds.Count; i++)
				NetUtil.H2N(clientIds[i], tmpBuffer, 5 + channelIds.Count * 8 + (i * 2));
			data.CopyTo(new Span<byte>(tmpBuffer, offset));

			packetHandler.AddOutgoingPacket(tmpBuffer, PacketType.VoiceWhisper);
		}

		public void SendAudioGroupWhisper(ReadOnlySpan<byte> data, Codec codec, GroupWhisperType type, GroupWhisperTarget target, ulong targetId = 0)
		{
			// [X,X,Y,N,M,U,U,U,U,U,U,U,U,DATA]
			// > X is a ushort in H2N order of a own audio packet counter
			//     it seems it can be the same as the packet counter so we will let the packethandler do it.
			// > Y is the codec byte (see Enum)
			// > N is a byte, specifying the GroupWhisperType
			// > M is a byte, specifying the GroupWhisperTarget
			// > U is a ulong in H2N order for the targeted channelId or groupId (0 if not applicable)
			byte[] tmpBuffer = new byte[data.Length + 13];
			tmpBuffer[2] = (byte)codec;
			tmpBuffer[3] = (byte)type;
			tmpBuffer[4] = (byte)target;
			NetUtil.H2N(targetId, tmpBuffer, 5);
			data.CopyTo(new Span<byte>(tmpBuffer, 13));

			packetHandler.AddOutgoingPacket(tmpBuffer, PacketType.VoiceWhisper, PacketFlags.Newprotocol);
		}

		public R<ConnectionInfo, CommandError> GetClientConnectionInfo(ClientIdT clientId)
		{
			var result = SendNotifyCommand(new Ts3Command("getconnectioninfo", new List<ICommandPart> {
				new CommandParameter("clid", clientId) }),
				NotificationType.ConnectionInfo);
			if (!result.Ok)
				return result.Error;
			return result.Value.Notifications
				.Cast<ConnectionInfo>()
				.Where(x => x.ClientId == clientId)
				.WrapSingle();
		}

		// serverrequestconnectioninfo
		// servergetvariables

		// Splitted base commands

		public override R<ServerGroupAddResponse, CommandError> ServerGroupAdd(string name, PermissionGroupDatabaseType? type = null)
		{
			var cmd = new Ts3Command("servergroupadd", new List<ICommandPart> { new CommandParameter("name", name) });
			if (type.HasValue)
				cmd.AppendParameter(new CommandParameter("type", (int)type.Value));
			var result = SendNotifyCommand(cmd, NotificationType.ServerGroupList);
			if (!result.Ok)
				return result.Error;
			return result.Value.Notifications
				.Cast<ServerGroupList>()
				.Where(x => x.Name == name)
				.Take(1)
				.Select(x => new ServerGroupAddResponse() { ServerGroupId = x.ServerGroupId })
				.WrapSingle();
		}

		public override R<IEnumerable<ClientServerGroup>, CommandError> ServerGroupsByClientDbId(ClientDbIdT clDbId)
		{
			var result = SendNotifyCommand(new Ts3Command("servergroupsbyclientid", new List<ICommandPart> {
				new CommandParameter("cldbid", clDbId) }),
				NotificationType.ServerGroupsByClientId);
			if (!result.Ok)
				return result.Error;

			return R<IEnumerable<ClientServerGroup>, CommandError>.OkR(
				result.Value.Notifications
				.Cast<ClientServerGroup>()
				.Where(x => x.ClientDbId == clDbId));
		}

		public override R<FileUpload, CommandError> FileTransferInitUpload(ChannelIdT channelId, string path, string channelPassword, ushort clientTransferId,
			long fileSize, bool overwrite, bool resume)
		{
			var result = SendNotifyCommand(new Ts3Command("ftinitupload", new List<ICommandPart>() {
				new CommandParameter("cid", channelId),
				new CommandParameter("name", path),
				new CommandParameter("cpw", channelPassword),
				new CommandParameter("clientftfid", clientTransferId),
				new CommandParameter("size", fileSize),
				new CommandParameter("overwrite", overwrite),
				new CommandParameter("resume", resume) }),
				NotificationType.StartUpload, NotificationType.FileTransferStatus);
			if (!result.Ok)
				return result.Error;
			if (result.Value.NotifyType == NotificationType.StartUpload)
				return result.UnwrapNotification<FileUpload>().WrapSingle();
			else
			{
				var ftresult = result.UnwrapNotification<FileTransferStatus>().WrapSingle();
				if (!ftresult)
					return ftresult.Error;
				return new CommandError() { Id = ftresult.Value.Status, Message = ftresult.Value.Message };
			}
		}

		public override R<FileDownload, CommandError> FileTransferInitDownload(ChannelIdT channelId, string path, string channelPassword, ushort clientTransferId,
			long seek)
		{
			var result = SendNotifyCommand(new Ts3Command("ftinitdownload", new List<ICommandPart>() {
				new CommandParameter("cid", channelId),
				new CommandParameter("name", path),
				new CommandParameter("cpw", channelPassword),
				new CommandParameter("clientftfid", clientTransferId),
				new CommandParameter("seekpos", seek) }), NotificationType.StartDownload, NotificationType.FileTransferStatus);
			if (!result.Ok)
				return result.Error;
			if (result.Value.NotifyType == NotificationType.StartDownload)
				return result.UnwrapNotification<FileDownload>().WrapSingle();
			else
			{
				var ftresult = result.UnwrapNotification<FileTransferStatus>().WrapSingle();
				if (!ftresult)
					return ftresult.Error;
				return new CommandError() { Id = ftresult.Value.Status, Message = ftresult.Value.Message };
			}
		}

		public override R<IEnumerable<FileTransfer>, CommandError> FileTransferList()
			=> SendNotifyCommand(new Ts3Command("ftlist"),
				NotificationType.FileTransfer).UnwrapNotification<FileTransfer>();

		public override R<IEnumerable<FileList>, CommandError> FileTransferGetFileList(ChannelIdT channelId, string path, string channelPassword = "")
			=> SendNotifyCommand(new Ts3Command("ftgetfilelist", new List<ICommandPart>() {
				new CommandParameter("cid", channelId),
				new CommandParameter("path", path),
				new CommandParameter("cpw", channelPassword) }),
				NotificationType.FileList).UnwrapNotification<FileList>();

		public override R<IEnumerable<FileInfoTs>, CommandError> FileTransferGetFileInfo(ChannelIdT channelId, string[] path, string channelPassword = "")
			=> SendNotifyCommand(new Ts3Command("ftgetfileinfo", new List<ICommandPart>() {
				new CommandParameter("cid", channelId),
				new CommandParameter("cpw", channelPassword),
				new CommandMultiParameter("name", path) }),
				NotificationType.FileInfo).UnwrapNotification<FileInfoTs>();

		#endregion

		private enum Ts3ClientStatus
		{
			Disconnected,
			Disconnecting,
			Connected,
			Connecting,
		}
	}

	internal class ConnectionContext
	{
		public bool WasExit { get; set; }
	}
}
