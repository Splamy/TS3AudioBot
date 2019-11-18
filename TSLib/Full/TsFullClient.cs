// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TSLib.Audio;
using TSLib.Commands;
using TSLib.Full.Book;
using TSLib.Helper;
using TSLib.Messages;
using CmdR = System.E<TSLib.Messages.CommandError>;

namespace TSLib.Full
{
	/// <summary>Creates a full TeamSpeak3 client with voice capabilities.</summary>
	public sealed partial class TsFullClient : TsBaseFunctions, IAudioActiveProducer, IAudioPassiveConsumer
	{
		private TsCrypt tsCrypt;
		private PacketHandler<S2C, C2S> packetHandler;
		private readonly AsyncMessageProcessor msgProc;

		private readonly object statusLock = new object();

		private int returnCode;
		private ConnectionContext context;

		private IEventDispatcher dispatcher;
		public override ClientType ClientType => ClientType.Full;
		/// <summary>The client id given to this connection by the server.</summary>
		public ClientId ClientId => packetHandler.ClientId;
		/// <summary>The disonnect message when leaving.</summary>
		public string QuitMessage { get; set; } = "Disconnected";
		/// <summary>The <see cref="Full.VersionSign"/> used to connect.</summary>
		public VersionSign VersionSign { get; private set; }
		/// <summary>The <see cref="Full.IdentityData"/> used to connect.</summary>
		public IdentityData Identity => tsCrypt.Identity;
		private TsClientStatus status;
		public override bool Connected { get { lock (statusLock) return status == TsClientStatus.Connected; } }
		public override bool Connecting { get { lock (statusLock) return status == TsClientStatus.Connecting; } }
		protected override Deserializer Deserializer => msgProc.Deserializer;
		private ConnectionDataFull connectionDataFull;
		public Connection Book { get; set; } = new Connection();

		public override event EventHandler<EventArgs> OnConnected;
		public override event EventHandler<DisconnectEventArgs> OnDisconnected;
		public event EventHandler<CommandError> OnErrorEvent;

		/// <summary>Creates a new client. A client can manage one connection to a server.</summary>
		/// <param name="dispatcherType">The message processing method for incomming notifications.
		/// See <see cref="EventDispatchType"/> for further information about each type.</param>
		public TsFullClient()
		{
			status = TsClientStatus.Disconnected;
			msgProc = new AsyncMessageProcessor(MessageHelper.GetToClientNotificationType);
			context = new ConnectionContext { WasExit = true };
		}

		/// <summary>Tries to connect to a server.</summary>
		/// <param name="conData">Set the connection information properties as needed.
		/// For further details about each setting see the respective property documentation in <see cref="ConnectionData"/></param>
		/// <exception cref="ArgumentException">When some required values are not set or invalid.</exception>
		/// <exception cref="TsException">When the connection could not be established.</exception>
		public override void Connect(ConnectionData conData)
		{
			if (!(conData is ConnectionDataFull conDataFull)) throw new ArgumentException($"Use the {nameof(ConnectionDataFull)} derivative to connect with the full client.", nameof(conData));
			if (conDataFull.Identity is null) throw new ArgumentNullException(nameof(conDataFull.Identity));
			if (conDataFull.VersionSign is null) throw new ArgumentNullException(nameof(conDataFull.VersionSign));
			connectionDataFull = conDataFull;
			ConnectionData = conData;

			Disconnect();

			if (!TsDnsResolver.TryResolve(conData.Address, out remoteAddress))
				throw new TsException("Could not read or resolve address.");

			lock (statusLock)
			{
				returnCode = 0;
				status = TsClientStatus.Connecting;

				VersionSign = conDataFull.VersionSign;
				tsCrypt = new TsCrypt();
				tsCrypt.Identity = conDataFull.Identity;

				var ctx = new ConnectionContext { WasExit = false };
				context = ctx;

				packetHandler = new PacketHandler<S2C, C2S>(tsCrypt, conData.LogId);
				packetHandler.PacketEvent = (ref Packet<S2C> packet) => { PacketEvent(ctx, ref packet); };
				packetHandler.StopEvent = (closeReason) => { ctx.ExitReason = closeReason; DisconnectInternal(ctx, setStatus: TsClientStatus.Disconnected); };
				packetHandler.Connect(remoteAddress);
				dispatcher = new ExtraThreadEventDispatcher();
				dispatcher.Init(InvokeEvent, conData.LogId);
			}
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

		private void DisconnectInternal(ConnectionContext ctx, CommandError error = null, TsClientStatus? setStatus = null)
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
				case TsClientStatus.Connecting:
				case TsClientStatus.Disconnected:
					ctx.WasExit = true;
					packetHandler.Stop();
					msgProc.DropQueue();
					dispatcher.Dispose();
					dispatcher = null;
					triggerEventSafe = true;
					break;
				case TsClientStatus.Disconnecting:
					break;
				case TsClientStatus.Connected:
					ClientDisconnect(Reason.LeftServer, QuitMessage);
					status = TsClientStatus.Disconnecting;
					break;
				default:
					throw Tools.UnhandledDefault(status);
				}
			}

			if (triggerEventSafe)
				OnDisconnected?.Invoke(this, new DisconnectEventArgs(ctx.ExitReason ?? Reason.LeftServer, error));
		}

		private void PacketEvent(ConnectionContext ctx, ref Packet<S2C> packet)
		{
			lock (statusLock)
			{
				if (ctx.WasExit)
					return;

				switch (packet.PacketType)
				{
				case PacketType.Command:
				case PacketType.CommandLow:
					Log.ConditionalDebug("[I] {0}", Tools.Utf8Encoder.GetString(packet.Data));
					var result = msgProc.PushMessage(packet.Data);
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

				case PacketType.Init1:
					// Init error
					if (packet.Data.Length == 5 && packet.Data[0] == 1)
					{
						var errorNum = BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan(1));
						if (Enum.IsDefined(typeof(TsErrorCode), errorNum))
							Log.Info("Got init error: {0}", (TsErrorCode)errorNum);
						else
							Log.Warn("Got undefined init error: {0}", errorNum);
						DisconnectInternal(ctx, setStatus: TsClientStatus.Disconnected);
					}
					break;
				}
			}
		}

		// Local event processing

		partial void ProcessEachInitIvExpand(InitIvExpand initIvExpand)
		{
			packetHandler.ReceivedFinalInitAck();

			var result = tsCrypt.CryptoInit(initIvExpand.Alpha, initIvExpand.Beta, initIvExpand.Omega);
			if (!result)
			{
				DisconnectInternal(context, CommandError.Custom($"Failed to calculate shared secret: {result.Error}"));
				return;
			}

			DefaultClientInit();
		}

		partial void ProcessEachInitIvExpand2(InitIvExpand2 initIvExpand2)
		{
			packetHandler.ReceivedFinalInitAck();

			var (publicKey, privateKey) = TsCrypt.GenerateTemporaryKey();

			var ekBase64 = Convert.ToBase64String(publicKey);
			var toSign = new byte[86];
			Array.Copy(publicKey, 0, toSign, 0, 32);
			var beta = Convert.FromBase64String(initIvExpand2.Beta);
			Array.Copy(beta, 0, toSign, 32, 54);
			var sign = TsCrypt.Sign(connectionDataFull.Identity.PrivateKey, toSign);
			var proof = Convert.ToBase64String(sign);
			ClientEk(ekBase64, proof);

			var result = tsCrypt.CryptoInit2(initIvExpand2.License, initIvExpand2.Omega, initIvExpand2.Proof, initIvExpand2.Beta, privateKey);
			if (!result)
			{
				DisconnectInternal(context, CommandError.Custom($"Failed to calculate shared secret: {result.Error}"));
				return;
			}

			DefaultClientInit();
		}

		partial void ProcessEachInitServer(InitServer initServer)
		{
			packetHandler.ClientId = initServer.ClientId;

			lock (statusLock)
				status = TsClientStatus.Connected;
			OnConnected?.Invoke(this, EventArgs.Empty);
		}

		partial void ProcessEachPluginCommand(PluginCommand cmd)
		{
			if (cmd.Name == "cliententerview" && cmd.Data == "version")
				SendPluginCommand("cliententerview", "TAB", PluginTargetMode.Server);
		}

		partial void ProcessEachCommandError(CommandError error)
		{
			bool skipError = false;
			bool disconnect = false;
			lock (statusLock)
			{
				if (status == TsClientStatus.Connecting)
				{
					disconnect = true;
					skipError = true;
				}
			}

			if (disconnect)
				DisconnectInternal(context, error, TsClientStatus.Disconnected);
			if (!skipError)
				OnErrorEvent?.Invoke(this, error);
		}

		partial void ProcessEachClientLeftView(ClientLeftView clientLeftView)
		{
			if (clientLeftView.ClientId == packetHandler.ClientId)
			{
				context.ExitReason = Reason.LeftServer;
				DisconnectInternal(context, setStatus: TsClientStatus.Disconnected);
			}
		}

		partial void ProcessEachChannelListFinished(ChannelListFinished _)
		{
			ChannelSubscribeAll();
			PermissionList();
		}

		partial void ProcessEachClientConnectionInfoUpdateRequest(ClientConnectionInfoUpdateRequest _)
		{
			SendNoResponsed(packetHandler.NetworkStats.GenerateStatusAnswer());
		}

		partial void ProcessPermList(PermList[] permList)
		{
			var buildPermissions = new List<TsPermission>(permList.Length + 1) { TsPermission.undefined };
			foreach (var perm in permList)
			{
				if (!string.IsNullOrEmpty(perm.PermissionName))
				{
					if (Enum.TryParse<TsPermission>(perm.PermissionName, out var tsPerm))
						buildPermissions.Add(tsPerm);
					else
						buildPermissions.Add(TsPermission.undefined);
				}
			}
			Deserializer.PermissionTransform = new TablePermissionTransform(buildPermissions.ToArray());
		}

		// ***

		private CmdR DefaultClientInit() => ClientInit(
			connectionDataFull.Username,
			true, true,
			connectionDataFull.DefaultChannel,
			connectionDataFull.DefaultChannelPassword.HashedPassword,
			connectionDataFull.ServerPassword.HashedPassword,
			string.Empty, string.Empty, string.Empty,
			connectionDataFull.Identity.ClientUid.Value, VersionSign);

		/// <summary>
		/// Sends a command to the server. Commands look exactly like query commands and mostly also behave identically.
		/// <para>NOTE: Do not expect all commands to work exactly like in the query documentation.</para>
		/// </summary>
		/// <typeparam name="T">The type to deserialize the response to. Use <see cref="ResponseDictionary"/> for unknow response data.</typeparam>
		/// <param name="com">The command to send.
		/// <para>NOTE: By default does the command expect an answer from the server. Set <see cref="TsCommand.ExpectResponse"/> to false
		/// if the client hangs after a special command (<see cref="Send{T}(TsCommand)"/> will return a generic error instead).</para></param>
		/// <returns>Returns <code>R(OK)</code> with an enumeration of the deserialized and split up in <see cref="T"/> objects data.
		/// Or <code>R(ERR)</code> with the returned error if no response is expected.</returns>
		public override R<T[], CommandError> Send<T>(TsCommand com)
		{
			using (var wb = new WaitBlock(msgProc.Deserializer, false))
			{
				var result = SendCommandBase(wb, com);
				if (!result.Ok)
					return result.Error;
				if (com.ExpectResponse)
					return wb.WaitForMessage<T>();
				else
					return Array.Empty<T>();
			}
		}

		/// <summary>
		/// Sends a command without expecting a 'error' return code.
		/// <para>NOTE: Do not use this method unless you are sure the ts3 command fits the criteria.</para>
		/// </summary>
		/// <param name="command">The command to send.</param>
		public CmdR SendNoResponsed(TsCommand command)
			=> SendVoid(command.ExpectsResponse(false));

		public override R<T[], CommandError> SendHybrid<T>(TsCommand com, NotificationType type)
			=> SendNotifyCommand(com, type).UnwrapNotification<T>();

		public R<LazyNotification, CommandError> SendNotifyCommand(TsCommand com, params NotificationType[] dependsOn)
		{
			if (!com.ExpectResponse)
				throw new ArgumentException("A special command must take a response");

			using (var wb = new WaitBlock(msgProc.Deserializer, false, dependsOn))
			{
				var result = SendCommandBase(wb, com);
				if (!result.Ok)
					return result.Error;
				return wb.WaitForNotification();
			}
		}

		private E<CommandError> SendCommandBase(WaitBlock wb, TsCommand com)
		{
			lock (statusLock)
			{
				if (context.WasExit || (!Connected && com.ExpectResponse))
					return CommandError.TimeOut;

				if (com.ExpectResponse)
				{
					var responseNumber = ++returnCode;
					var retCodeParameter = new CommandParameter("return_code", responseNumber);
					com.Add(retCodeParameter);
					msgProc.EnqueueRequest(retCodeParameter.Value, wb);
				}

				var message = com.ToString();
				Log.Debug("[O] {0}", message);
				byte[] data = Tools.Utf8Encoder.GetBytes(message);
				packetHandler.AddOutgoingPacket(data, PacketType.Command);
			}
			return R.Ok;
		}

		public async Task<R<T[], CommandError>> SendCommandAsync<T>(TsCommand com) where T : IResponse, new()
		{
			using (var wb = new WaitBlock(msgProc.Deserializer, true))
			{
				var result = SendCommandBase(wb, com);
				if (!result.Ok)
					return result.Error;
				if (com.ExpectResponse)
					return await wb.WaitForMessageAsync<T>().ConfigureAwait(false);
				else
					// This might not be the nicest way to return in this case
					// but we don't know what the response is, so this acceptable.
					return CommandError.NoResult;
			}
		}

		/// <summary>Release all resources. Will try to disconnect before disposing.</summary>
		public override void Dispose()
		{
			Disconnect();
		}

		#region Audio
		/// <summary>Receive voice packets.</summary>
		public IAudioPassiveConsumer OutStream { get; set; }
		/// <summary>When voice data can be sent.</summary>
		// TODO may set to false if no talk power, etc.
		public bool Active => true;
		/// <summary>Send voice data.</summary>
		/// <param name="data">The encoded audio buffer.</param>
		/// <param name="meta">The metadata where to send the packet.</param>
		public void Write(Span<byte> data, Meta meta)
		{
			if (meta.Out is null
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
			=> SendVoid(new TsCommand("clientupdate") {
				{ "client_is_channel_commander", isChannelCommander },
			});

		public CmdR RequestTalkPower(string message = null)
			=> SendVoid(new TsCommand("clientupdate") {
				{ "client_talk_request", true },
				{ "client_talk_request_msg", message },
			});

		public CmdR CancelTalkPowerRequest()
			=> SendVoid(new TsCommand("clientupdate") {
				{ "client_talk_request", false },
			});

		public CmdR ClientEk(string ek, string proof)
			=> SendNoResponsed(new TsCommand("clientek") {
				{ "ek", ek },
				{ "proof", proof },
			});

		public CmdR ClientInit(string nickname, bool inputHardware, bool outputHardware,
				string defaultChannel, string defaultChannelPassword, string serverPassword, string metaData,
				string nicknamePhonetic, string defaultToken, string hwid, VersionSign versionSign)
			=> SendNoResponsed(new TsCommand("clientinit") {
				{ "client_nickname", nickname },
				{ "client_version", versionSign.Name },
				{ "client_platform", versionSign.PlatformName },
				{ "client_input_hardware", inputHardware },
				{ "client_output_hardware", outputHardware },
				{ "client_default_channel", defaultChannel },
				{ "client_default_channel_password", defaultChannelPassword }, // base64(sha1(pass))
				{ "client_server_password", serverPassword }, // base64(sha1(pass))
				{ "client_meta_data", metaData },
				{ "client_version_sign", versionSign.Sign },
				{ "client_key_offset", Identity.ValidKeyOffset },
				{ "client_nickname_phonetic", nicknamePhonetic },
				{ "client_default_token", defaultToken },
				{ "hwid", hwid },
			});

		public CmdR ClientDisconnect(Reason reason, string reasonMsg)
			=> SendNoResponsed(new TsCommand("clientdisconnect") {
				{ "reasonid", (int)reason },
				{ "reasonmsg", reasonMsg }
			});

		public CmdR ChannelSubscribeAll()
			=> SendVoid(new TsCommand("channelsubscribeall"));

		public CmdR ChannelUnsubscribeAll()
			=> SendVoid(new TsCommand("channelunsubscribeall"));

		public CmdR PokeClient(string message, ushort clientId)
			=> SendNoResponsed(new TsCommand("clientpoke") {
				{ "clid", clientId },
				{ "msg", message },
			});

		public void SendAudio(in ReadOnlySpan<byte> data, Codec codec)
		{
			// [X,X,Y,DATA]
			// > X is a ushort in H2N order of an own audio packet counter
			//     it seems it can be the same as the packet counter so we will let the packethandler do it.
			// > Y is the codec byte (see Enum)
			Span<byte> tmpBuffer = stackalloc byte[data.Length + 3];
			tmpBuffer[2] = (byte)codec;
			data.CopyTo(tmpBuffer.Slice(3));

			packetHandler.AddOutgoingPacket(tmpBuffer, PacketType.Voice);
		}

		public void SendAudioWhisper(in ReadOnlySpan<byte> data, Codec codec, IReadOnlyList<ChannelId> channelIds, IReadOnlyList<ClientId> clientIds)
		{
			// [X,X,Y,N,M,(U,U,U,U,U,U,U,U)*,(T,T)*,DATA]
			// > X is a ushort in H2N order of an own audio packet counter
			//     it seems it can be the same as the packet counter so we will let the packethandler do it.
			// > Y is the codec byte (see Enum)
			// > N is a byte, the count of ChannelIds to send to
			// > M is a byte, the count of ClientIds to send to
			// > U is a ulong in H2N order of each targeted channelId, (U...U) is repeated N times
			// > T is a ushort in H2N order of each targeted clientId, (T...T) is repeated M times
			int offset = 2 + 1 + 2 + channelIds.Count * 8 + clientIds.Count * 2;
			Span<byte> tmpBuffer = stackalloc byte[data.Length + offset];
			tmpBuffer[2] = (byte)codec;
			tmpBuffer[3] = (byte)channelIds.Count;
			tmpBuffer[4] = (byte)clientIds.Count;
			for (int i = 0; i < channelIds.Count; i++)
				BinaryPrimitives.WriteUInt64BigEndian(tmpBuffer.Slice(5 + (i * 8)), channelIds[i].Value);
			for (int i = 0; i < clientIds.Count; i++)
				BinaryPrimitives.WriteUInt16BigEndian(tmpBuffer.Slice(5 + channelIds.Count * 8 + (i * 2)), clientIds[i].Value);
			data.CopyTo(tmpBuffer.Slice(offset));

			packetHandler.AddOutgoingPacket(tmpBuffer, PacketType.VoiceWhisper);
		}

		public void SendAudioGroupWhisper(in ReadOnlySpan<byte> data, Codec codec, GroupWhisperType type, GroupWhisperTarget target, ulong targetId = 0)
		{
			// [X,X,Y,N,M,U,U,U,U,U,U,U,U,DATA]
			// > X is a ushort in H2N order of an own audio packet counter
			//     it seems it can be the same as the packet counter so we will let the packethandler do it.
			// > Y is the codec byte (see Enum)
			// > N is a byte, specifying the GroupWhisperType
			// > M is a byte, specifying the GroupWhisperTarget
			// > U is a ulong in H2N order for the targeted channelId or groupId (0 if not applicable)
			Span<byte> tmpBuffer = stackalloc byte[data.Length + 13];
			tmpBuffer[2] = (byte)codec;
			tmpBuffer[3] = (byte)type;
			tmpBuffer[4] = (byte)target;
			BinaryPrimitives.WriteUInt64BigEndian(tmpBuffer.Slice(5), targetId);
			data.CopyTo(tmpBuffer.Slice(13));

			packetHandler.AddOutgoingPacket(tmpBuffer, PacketType.VoiceWhisper, PacketFlags.Newprotocol);
		}

		public R<ClientConnectionInfo, CommandError> GetClientConnectionInfo(ClientId clientId)
		{
			var result = SendNotifyCommand(new TsCommand("getconnectioninfo") {
				{ "clid", clientId }
			}, NotificationType.ClientConnectionInfo);
			if (!result.Ok)
				return result.Error;
			return result.Value.Notifications
				.Cast<ClientConnectionInfo>()
				.Where(x => x.ClientId == clientId)
				.WrapSingle();
		}

		public R<ClientUpdated, CommandError> GetClientVariables(ushort clientId)
			=> SendNotifyCommand(new TsCommand("clientgetvariables") {
				{ "clid", clientId }
			}, NotificationType.ClientUpdated).UnwrapNotification<ClientUpdated>().WrapSingle();

		public R<ServerUpdated, CommandError> GetServerVariables()
			=> SendNotifyCommand(new TsCommand("servergetvariables"),
				NotificationType.ServerUpdated).UnwrapNotification<ServerUpdated>().WrapSingle();

		public CmdR SendPluginCommand(string name, string data, PluginTargetMode targetmode)
			=> SendVoid(new TsCommand("plugincmd") {
				{ "name", name },
				{ "data", data },
				{ "targetmode", (int)targetmode },
			});

		// Splitted base commands

		public override R<IChannelCreateResponse, CommandError> ChannelCreate(string name,
			string namePhonetic = null, string topic = null, string description = null, string password = null,
			Codec? codec = null, int? codecQuality = null, int? codecLatencyFactor = null, bool? codecEncrypted = null,
			int? maxClients = null, int? maxFamilyClients = null, bool? maxClientsUnlimited = null,
			bool? maxFamilyClientsUnlimited = null, bool? maxFamilyClientsInherited = null, ChannelId? order = null,
			ChannelId? parent = null, ChannelType? type = null, TimeSpan? deleteDelay = null, int? neededTalkPower = null)
			=> SendNotifyCommand(ChannelOp("channelcreate", null, name, namePhonetic, topic, description,
				password, codec, codecQuality, codecLatencyFactor, codecEncrypted,
				maxClients, maxFamilyClients, maxClientsUnlimited, maxFamilyClientsUnlimited,
				maxFamilyClientsInherited, order, parent, type, deleteDelay, neededTalkPower),
				NotificationType.ChannelCreated)
				.UnwrapNotification<ChannelCreated>()
				.WrapSingle()
				.WrapInterface<ChannelCreated, IChannelCreateResponse>();

		public override R<ServerGroupAddResponse, CommandError> ServerGroupAdd(string name, GroupType? type = null)
		{
			var result = SendNotifyCommand(new TsCommand("servergroupadd") {
				{ "name", name },
				{ "type", (int?)type }
			}, NotificationType.ServerGroupList);
			if (!result.Ok)
				return result.Error;
			return result.Value.Notifications
				.Cast<ServerGroupList>()
				.Where(x => x.Name == name)
				.Take(1)
				.Select(x => new ServerGroupAddResponse() { ServerGroupId = x.ServerGroupId })
				.WrapSingle();
		}

		public override R<FileUpload, CommandError> FileTransferInitUpload(ChannelId channelId, string path, string channelPassword, ushort clientTransferId,
			long fileSize, bool overwrite, bool resume)
		{
			var result = SendNotifyCommand(new TsCommand("ftinitupload") {
				{ "cid", channelId },
				{ "name", path },
				{ "cpw", channelPassword },
				{ "clientftfid", clientTransferId },
				{ "size", fileSize },
				{ "overwrite", overwrite },
				{ "resume", resume }
			}, NotificationType.FileUpload, NotificationType.FileTransferStatus);
			if (!result.Ok)
				return result.Error;
			if (result.Value.NotifyType == NotificationType.FileUpload)
				return result.UnwrapNotification<FileUpload>().WrapSingle();
			else
			{
				var ftresult = result.UnwrapNotification<FileTransferStatus>().WrapSingle();
				if (!ftresult)
					return ftresult.Error;
				return new CommandError() { Id = ftresult.Value.Status, Message = ftresult.Value.Message };
			}
		}

		public override R<FileDownload, CommandError> FileTransferInitDownload(ChannelId channelId, string path, string channelPassword, ushort clientTransferId,
			long seek)
		{
			var result = SendNotifyCommand(new TsCommand("ftinitdownload") {
				{ "cid", channelId },
				{ "name", path },
				{ "cpw", channelPassword },
				{ "clientftfid", clientTransferId },
				{ "seekpos", seek } }, NotificationType.FileDownload, NotificationType.FileTransferStatus);
			if (!result.Ok)
				return result.Error;
			if (result.Value.NotifyType == NotificationType.FileDownload)
				return result.UnwrapNotification<FileDownload>().WrapSingle();
			else
			{
				var ftresult = result.UnwrapNotification<FileTransferStatus>().WrapSingle();
				if (!ftresult)
					return ftresult.Error;
				return new CommandError() { Id = ftresult.Value.Status, Message = ftresult.Value.Message };
			}
		}

		#endregion

		private enum TsClientStatus
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
		public Reason? ExitReason { get; set; }
	}
}
