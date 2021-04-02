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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TSLib.Audio;
using TSLib.Commands;
using TSLib.Full.Book;
using TSLib.Helper;
using TSLib.Messages;
using TSLib.Scheduler;
using CmdR = System.Threading.Tasks.Task<System.E<TSLib.Messages.CommandError>>;

namespace TSLib.Full
{
	/// <summary>Creates a full TeamSpeak3 client with voice capabilities.</summary>
	public sealed partial class TsFullClient : TsBaseFunctions, IAudioActiveProducer, IAudioPassiveConsumer
	{
		private readonly AsyncMessageProcessor msgProc;
		private readonly DedicatedTaskScheduler scheduler;
		private readonly bool isOwnScheduler;

		private uint returnCode;
		private ConnectionContext? context;

		public override ClientType ClientType => ClientType.Full;
		/// <summary>The client id given to this connection by the server.</summary>
		public ClientId ClientId => context?.PacketHandler.ClientId ?? ClientId.Null;
		/// <summary>The disonnect message when leaving.</summary>
		public string QuitMessage { get; set; } = "Disconnected";
		/// <summary>The <see cref="TsVersionSigned"/> used to connect.</summary>
		public TsVersionSigned? VersionSign => context?.ConnectionDataFull.VersionSign;
		/// <summary>The <see cref="IdentityData"/> used to connect.</summary>
		public IdentityData? Identity => context?.ConnectionDataFull.Identity;
		/// <summary>
		/// Status overview:
		/// <list type="bullet">
		/// <item> Disconnected:
		///   <para> ! PacketHandler is not initalized, context == null</para>
		///   <para> -> Connect() => Connecting</para>
		/// </item>
		/// <item> Connecting:
		///   <para> -> Init/Crypto-Error => Disconnected</para>
		///   <para> -> Timeout => Disconnected</para>
		///   <para> -> Final Init => Connected</para>
		/// </item>
		/// <item> Connected:
		///   <para> -> Timeout => Disconnected</para>
		///   <para> -> Kick/Leave => Disconnecting</para>
		/// </item>
		/// <item> Disconnecting:
		///   <para> -> Timeout => Disconnected</para>
		///   <para> -> Final Ack => Disconnected</para>
		/// </item>
		/// </list>
		/// </summary>
		private TsClientStatus status;
		public override bool Connected => status == TsClientStatus.Connected;
		public override bool Connecting => status == TsClientStatus.Connecting;
		protected override Deserializer Deserializer => msgProc.Deserializer;
		public Connection Book { get; } = new Connection();

		public override event EventHandler<DisconnectEventArgs>? OnDisconnected;
		public event EventHandler<CommandError>? OnErrorEvent;

		/// <summary>Creates a new client. A client can manage one connection to a server.</summary>
		/// <param name="dispatcherType">The message processing method for incomming notifications.
		/// See <see cref="EventDispatchType"/> for further information about each type.</param>
		public TsFullClient(DedicatedTaskScheduler? scheduler = null)
		{
			status = TsClientStatus.Disconnected;
			msgProc = new AsyncMessageProcessor(MessageHelper.GetToClientNotificationType);
			this.scheduler = scheduler ?? new DedicatedTaskScheduler(Id.Null);
			this.isOwnScheduler = scheduler is null;
		}

		/// <summary>Tries to connect to a server.</summary>
		/// <param name="conData">Set the connection information properties as needed.
		/// For further details about each setting see the respective property documentation in <see cref="ConnectionData"/></param>
		/// <exception cref="ArgumentException">When some required values are not set or invalid.</exception>
		/// <exception cref="TsException">When the connection could not be established.</exception>
		public override async CmdR Connect(ConnectionData conData)
		{
			scheduler.VerifyOwnThread();
			if (!(conData is ConnectionDataFull conDataFull)) throw new ArgumentException($"Use the {nameof(ConnectionDataFull)} derivative to connect with the full client.", nameof(conData));
			if (conDataFull.Identity is null) throw new ArgumentNullException(nameof(conDataFull.Identity));
			if (conDataFull.VersionSign is null) throw new ArgumentNullException(nameof(conDataFull.VersionSign));

			await Disconnect();

			remoteAddress = await TsDnsResolver.TryResolve(conData.Address);
			if (remoteAddress is null)
				return CommandError.Custom("Could not read or resolve address.");

			ConnectionData = conData;
			ServerConstants = TsConst.Default;
			Book.Reset();
			returnCode = 0;

			var ctx = new ConnectionContext(conDataFull);
			context = ctx;

			ctx.PacketHandler.PacketEvent = (ref Packet<S2C> packet) =>
			{
				if (status == TsClientStatus.Disconnected)
					return;
				PacketEvent(ctx, ref packet);
			};
			ctx.PacketHandler.StopEvent = (closeReason) =>
			{
				_ = scheduler.Invoke(() =>
				{
					ctx.ExitReason ??= closeReason;
					ChangeState(ctx, TsClientStatus.Disconnected);
				});
			};

			ChangeState(ctx, TsClientStatus.Connecting);
			if (!ctx.PacketHandler.Connect(remoteAddress).GetOk(out var error))
			{
				ChangeState(ctx, TsClientStatus.Disconnected);
				return CommandError.Custom(error);
			}
			return await ctx.ConnectEvent.Task; // TODO check error state
		}

		/// <summary>
		/// Disconnects from the current server and closes the connection.
		/// Does nothing if the client is not connected.
		/// </summary>
		public override async Task Disconnect()
		{
			scheduler.VerifyOwnThread();

			var ctx = context;
			if (ctx is null)
				return;

			// TODO: Consider if it is better when in connecting state to wait for connect completion then disconnect
			if (status == TsClientStatus.Connected)
			{
				await ClientDisconnect(Reason.LeftServer, QuitMessage);
				ChangeState(ctx, TsClientStatus.Disconnecting);
			}
			else
			{
				ChangeState(ctx, TsClientStatus.Disconnected);
			}
			await ctx.DisconnectEvent.Task;
		}

		private void ChangeState(ConnectionContext ctx, TsClientStatus setStatus, CommandError? error = null)
		{
			scheduler.VerifyOwnThread();

			if (ctx != context)
				Log.Debug("Stray disconnect from old packethandler");

			Log.Debug("ChangeState {0} -> {1} (error:{2})", status, setStatus, error?.ErrorFormat() ?? "none");

			switch ((status, setStatus))
			{
			case (TsClientStatus.Disconnected, TsClientStatus.Disconnected):
				// Already disconnected, do nothing
				break;

			case (TsClientStatus.Disconnected, TsClientStatus.Connecting):
				status = TsClientStatus.Connecting;
				break;

			case (TsClientStatus.Connecting, TsClientStatus.Connected):
				status = TsClientStatus.Connected;
				ctx.ConnectEvent.SetResult(R.Ok);
				break;

			case (TsClientStatus.Connecting, TsClientStatus.Disconnected):
			case (TsClientStatus.Connected, TsClientStatus.Disconnected):
			case (TsClientStatus.Disconnecting, TsClientStatus.Disconnected):
				status = TsClientStatus.Disconnected;
				ctx.PacketHandler.Stop();
				msgProc.DropQueue();

				var statusBefore = status;
				context = null;
				if (statusBefore == TsClientStatus.Connecting)
					ctx.ConnectEvent.SetResult(error ?? CommandError.ConnectionClosed); // TODO: Set exception maybe ?
				ctx.DisconnectEvent.SetResult(null);
				OnDisconnected?.Invoke(this, new DisconnectEventArgs(ctx.ExitReason ?? Reason.LeftServer, error));
				break;

			case (TsClientStatus.Connected, TsClientStatus.Disconnecting):
				status = TsClientStatus.Disconnecting;
				break;

			default:
				Trace.Fail($"Invalid transition change from {status} to {setStatus}");
				break;
			}
		}

		private void PacketEvent(ConnectionContext ctx, ref Packet<S2C> packet)
		{
			switch (packet.PacketType)
			{
			case PacketType.Command:
			case PacketType.CommandLow:
				var data = packet.Data;
				if (Log.IsDebugEnabled)
					Log.Debug("[I] {0}", Tools.Utf8Encoder.GetString(packet.Data));
				_ = scheduler.Invoke(() =>
				{
					if (ctx != context)
						Log.Debug("Stray packet from old packethandler");

					var result = msgProc.PushMessage(data);
					if (result != null)
						InvokeEvent(result.Value);
				});
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
					_ = scheduler.Invoke(() => ChangeState(ctx, TsClientStatus.Disconnected));
				}
				break;
			}
		}

		// Local event processing

		async partial void ProcessEachInitIvExpand(InitIvExpand initIvExpand)
		{
			var ctx = context;
			if (ctx is null) throw new InvalidOperationException("context should be set");

			ctx.PacketHandler.ReceivedFinalInitAck();

			var result = ctx.TsCrypt.CryptoInit(initIvExpand.Alpha, initIvExpand.Beta, initIvExpand.Omega);
			if (!result)
			{
				ChangeState(ctx, TsClientStatus.Disconnected, CommandError.Custom($"Failed to calculate shared secret: {result.Error}"));
				return;
			}

			await DefaultClientInit(ctx);
		}

		async partial void ProcessEachInitIvExpand2(InitIvExpand2 initIvExpand2)
		{
			var ctx = context;
			if (ctx is null) throw new InvalidOperationException("context should be set");

			ctx.PacketHandler.ReceivedFinalInitAck();

			var (publicKey, privateKey) = TsCrypt.GenerateTemporaryKey();

			var ekBase64 = Convert.ToBase64String(publicKey);
			var toSign = new byte[86];
			Array.Copy(publicKey, 0, toSign, 0, 32);
			var beta = Convert.FromBase64String(initIvExpand2.Beta);
			Array.Copy(beta, 0, toSign, 32, 54);
			var sign = TsCrypt.Sign(ctx.ConnectionDataFull.Identity.PrivateKey, toSign);
			var proof = Convert.ToBase64String(sign);
			await ClientEk(ekBase64, proof);

			var result = ctx.TsCrypt.CryptoInit2(initIvExpand2.License, initIvExpand2.Omega, initIvExpand2.Proof, initIvExpand2.Beta, privateKey);
			if (!result)
			{
				ChangeState(ctx, TsClientStatus.Disconnected, CommandError.Custom($"Failed to calculate shared secret: {result.Error}"));
				return;
			}

			await DefaultClientInit(ctx);
		}

		partial void ProcessEachInitServer(InitServer initServer)
		{
			var ctx = context;
			if (ctx is null) throw new InvalidOperationException("context should be set");

			ctx.PacketHandler.ClientId = initServer.ClientId;
			var serverVersion = TsVersion.TryParse(initServer.ServerVersion, initServer.ServerPlatform);
			if (serverVersion != null)
				ServerConstants = TsConst.GetByServerBuildNum(serverVersion.Build);

			ChangeState(ctx, TsClientStatus.Connected);

		}

		async partial void ProcessEachPluginCommand(PluginCommand cmd)
		{
			if (cmd.Name == "cliententerview" && cmd.Data == "version")
				await SendPluginCommand("cliententerview", "TAB", PluginTargetMode.Server);
		}

		partial void ProcessEachCommandError(CommandError error)
		{
			var ctx = context;
			if (ctx is null) throw new InvalidOperationException("context should be set");

			if (status == TsClientStatus.Connecting)
				ChangeState(ctx, TsClientStatus.Disconnected, error);
			else
				OnErrorEvent?.Invoke(this, error);
		}

		partial void ProcessEachClientLeftView(ClientLeftView clientLeftView)
		{
			var ctx = context;
			if (ctx is null) throw new InvalidOperationException("context should be set");

			if (clientLeftView.ClientId == ctx.PacketHandler.ClientId)
			{
				ctx.ExitReason = clientLeftView.Reason;
				ChangeState(ctx, TsClientStatus.Disconnected);
			}
		}

		async partial void ProcessEachChannelListFinished(ChannelListFinished _)
		{
			await ChannelSubscribeAll();
			await PermissionList();
		}

		async partial void ProcessEachClientConnectionInfoUpdateRequest(ClientConnectionInfoUpdateRequest _)
		{
			if (context is null) throw new InvalidOperationException("context should be set");

			await SendNoResponsed(context.PacketHandler.NetworkStats.GenerateStatusAnswer());
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

		private Task DefaultClientInit(ConnectionContext context)
		{
			var cdf = context.ConnectionDataFull;
			return ClientInit(
				cdf.Username,
				true, true,
				cdf.DefaultChannel,
				cdf.DefaultChannelPassword.HashedPassword,
				cdf.ServerPassword.HashedPassword,
				string.Empty, string.Empty, string.Empty,
				cdf.Identity.ClientUid.ToString(), cdf.VersionSign, cdf.Identity.ValidKeyOffset);
		}

		// ***

		/// <summary>
		/// Sends a command without expecting a 'error' return code.
		/// <para>NOTE: Do not use this method unless you are sure the ts3 command fits the criteria.</para>
		/// </summary>
		/// <param name="command">The command to send.</param>
		public Task SendNoResponsed(TsCommand command)
		{
			return SendVoid(command.ExpectsResponse(false));
		}

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
		public override async Task<R<T[], CommandError>> Send<T>(TsCommand com)
		{
			using var wb = new WaitBlock(msgProc.Deserializer);
			var result = SendCommandBase(wb, com);
			if (!result.Ok)
				return result.Error;
			if (com.ExpectResponse)
				return await wb.WaitForMessageAsync<T>();
			else
				// This might not be the nicest way to return in this case
				// but we don't know what the response is, so this acceptable.
				return CommandError.NoResult;
		}

		public override async Task<R<T[], CommandError>> SendHybrid<T>(TsCommand com, NotificationType type)
		{
			var notification = await SendNotifyCommand(com, type);
			return notification.UnwrapNotification<T>();
		}

		public async Task<R<LazyNotification, CommandError>> SendNotifyCommand(TsCommand com, params NotificationType[] dependsOn)
		{
			if (!com.ExpectResponse)
				throw new ArgumentException("A special command must take a response");

			using var wb = new WaitBlock(msgProc.Deserializer, dependsOn);
			var result = SendCommandBase(wb, com);
			if (!result.Ok)
				return result.Error;
			return await wb.WaitForNotificationAsync();
		}

		private E<CommandError> SendCommandBase(WaitBlock wb, TsCommand com)
		{
			scheduler.VerifyOwnThread();

			if (status != TsClientStatus.Connecting && status != TsClientStatus.Connected)
				return CommandError.ConnectionClosed;

			if (context is null) throw new InvalidOperationException("context should be set");

			if (com.ExpectResponse)
			{
				var responseNumber = unchecked(++returnCode);
				var retCodeParameter = new CommandParameter("return_code", responseNumber);
				com.Add(retCodeParameter);
				msgProc.EnqueueRequest(retCodeParameter.Value, wb);
			}

			var message = com.ToString();
			Log.Debug("[O] {0}", message);
			byte[] data = Tools.Utf8Encoder.GetBytes(message);
			var sendResult = context.PacketHandler.AddOutgoingPacket(data, PacketType.Command);
			if (!sendResult)
				Log.Debug("packetHandler couldn't send packet: {0}", sendResult.Error);
			return R.Ok;
		}

		/// <summary>Release all resources. Does not wait for a normal disconnect. Await Disconnect for this instead.</summary>
		public override void Dispose()
		{
			context?.PacketHandler.Stop();
			if (isOwnScheduler && scheduler is IDisposable disp)
				disp.Dispose();
		}

		#region Audio
		/// <summary>Receive voice packets.</summary>
		public IAudioPassiveConsumer? OutStream { get; set; }
		/// <summary>When voice data can be sent.</summary>
		// TODO may set to false if no talk power, etc.
		public bool Active => true;
		/// <summary>Send voice data.</summary>
		/// <param name="data">The encoded audio buffer.</param>
		/// <param name="meta">The metadata where to send the packet.</param>
		public void Write(Span<byte> data, Meta? meta)
		{
			if (meta?.Out is null
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
				SendAudioWhisper(data, meta.Codec.Value, meta.Out.ChannelIds!, meta.Out.ClientIds!);
				break;
			case TargetSendMode.WhisperGroup:
				SendAudioGroupWhisper(data, meta.Codec.Value, meta.Out.GroupWhisperType, meta.Out.GroupWhisperTarget, meta.Out.TargetId);
				break;
			default: throw Tools.UnhandledDefault(meta.Out.SendMode);
			}
		}
		#endregion

		#region FULLCLIENT SPECIFIC COMMANDS

		public CmdR ChangeIsChannelCommander(bool isChannelCommander)
			=> SendVoid(new TsCommand("clientupdate") {
				{ "client_is_channel_commander", isChannelCommander },
			});

		public CmdR ChangeDescription(string newDescription)
			=> ChangeDescription(newDescription, ClientId);

		public CmdR RequestTalkPower(string? message = null)
			=> SendVoid(new TsCommand("clientupdate") {
				{ "client_talk_request", true },
				{ "client_talk_request_msg", message },
			});

		public CmdR CancelTalkPowerRequest()
			=> SendVoid(new TsCommand("clientupdate") {
				{ "client_talk_request", false },
			});

		public Task ClientEk(string ek, string proof)
			=> SendNoResponsed(new TsCommand("clientek") {
				{ "ek", ek },
				{ "proof", proof },
			});

		public Task ClientInit(string nickname, bool inputHardware, bool outputHardware,
				string defaultChannel, string defaultChannelPassword, string serverPassword, string metaData,
				string nicknamePhonetic, string defaultToken, string hwid, TsVersionSigned versionSign, ulong keyOffset)
			=> SendNoResponsed(new TsCommand("clientinit") {
				{ "client_nickname", nickname },
				{ "client_version", versionSign.Version },
				{ "client_platform", versionSign.Platform },
				{ "client_input_hardware", inputHardware },
				{ "client_output_hardware", outputHardware },
				{ "client_default_channel", defaultChannel },
				{ "client_default_channel_password", defaultChannelPassword }, // base64(sha1(pass))
				{ "client_server_password", serverPassword }, // base64(sha1(pass))
				{ "client_meta_data", metaData },
				{ "client_version_sign", versionSign.Sign },
				{ "client_key_offset", keyOffset },
				{ "client_nickname_phonetic", nicknamePhonetic },
				{ "client_default_token", defaultToken },
				{ "hwid", hwid },
			});

		public Task ClientDisconnect(Reason reason, string reasonMsg)
			=> SendNoResponsed(new TsCommand("clientdisconnect") {
				{ "reasonid", (int)reason },
				{ "reasonmsg", reasonMsg }
			});

		public CmdR ChannelSubscribeAll()
			=> SendVoid(new TsCommand("channelsubscribeall"));

		public CmdR ChannelUnsubscribeAll()
			=> SendVoid(new TsCommand("channelunsubscribeall"));

		public Task PokeClient(string message, ClientId clientId)
			=> SendNoResponsed(new TsCommand("clientpoke") {
				{ "clid", clientId },
				{ "msg", message },
			});

		public void SendAudio(in ReadOnlySpan<byte> data, Codec codec)
		{
			var ctx = context;
			if (ctx is null) return;

			// [X,X,Y,DATA]
			// > X is a ushort in H2N order of an own audio packet counter
			//     it seems it can be the same as the packet counter so we will let the packethandler do it.
			// > Y is the codec byte (see Enum)
			Span<byte> tmpBuffer = stackalloc byte[data.Length + 3];
			tmpBuffer[2] = (byte)codec;
			data.CopyTo(tmpBuffer.Slice(3));

			ctx.PacketHandler.AddOutgoingPacket(tmpBuffer, PacketType.Voice);
		}

		public void SendAudioWhisper(in ReadOnlySpan<byte> data, Codec codec, IReadOnlyList<ChannelId> channelIds, IReadOnlyList<ClientId> clientIds)
		{
			var ctx = context;
			if (ctx is null) return;

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

			ctx.PacketHandler.AddOutgoingPacket(tmpBuffer, PacketType.VoiceWhisper);
		}

		public void SendAudioGroupWhisper(in ReadOnlySpan<byte> data, Codec codec, GroupWhisperType type, GroupWhisperTarget target, ulong targetId = 0)
		{
			var ctx = context;
			if (ctx is null) return;

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

			ctx.PacketHandler.AddOutgoingPacket(tmpBuffer, PacketType.VoiceWhisper, PacketFlags.Newprotocol);
		}

		public async Task<R<ClientConnectionInfo, CommandError>> GetClientConnectionInfo(ClientId clientId)
		{
			var result = await SendNotifyCommand(new TsCommand("getconnectioninfo") {
				{ "clid", clientId }
			}, NotificationType.ClientConnectionInfo);
			if (!result.Ok)
				return result.Error;
			return result.Value.Notifications
				.Cast<ClientConnectionInfo>()
				.Where(x => x.ClientId == clientId)
				.MapToSingle();
		}

		public async Task<R<ClientUpdated, CommandError>> GetClientVariables(ushort clientId)
			=> await SendNotifyCommand(new TsCommand("clientgetvariables") {
				{ "clid", clientId }
			}, NotificationType.ClientUpdated).MapToSingle<ClientUpdated>();

		public Task<R<ServerUpdated, CommandError>> GetServerVariables()
			=> SendNotifyCommand(new TsCommand("servergetvariables"),
				NotificationType.ServerUpdated).MapToSingle<ServerUpdated>();

		public CmdR SendPluginCommand(string name, string data, PluginTargetMode targetmode)
			=> SendVoid(new TsCommand("plugincmd") {
				{ "name", name },
				{ "data", data },
				{ "targetmode", (int)targetmode },
			});

		// Splitted base commands

		public override async Task<R<IChannelCreateResponse, CommandError>> ChannelCreate(string name,
			string? namePhonetic = null, string? topic = null, string? description = null, string? password = null,
			Codec? codec = null, int? codecQuality = null, int? codecLatencyFactor = null, bool? codecEncrypted = null,
			int? maxClients = null, int? maxFamilyClients = null, bool? maxClientsUnlimited = null,
			bool? maxFamilyClientsUnlimited = null, bool? maxFamilyClientsInherited = null, ChannelId? order = null,
			ChannelId? parent = null, ChannelType? type = null, TimeSpan? deleteDelay = null, int? neededTalkPower = null)
		{
			var result = await SendNotifyCommand(ChannelOp("channelcreate", null, name, namePhonetic, topic, description,
				  password, codec, codecQuality, codecLatencyFactor, codecEncrypted,
				  maxClients, maxFamilyClients, maxClientsUnlimited, maxFamilyClientsUnlimited,
				  maxFamilyClientsInherited, order, parent, type, deleteDelay, neededTalkPower),
				  NotificationType.ChannelCreated);
			return result.UnwrapNotification<ChannelCreated>()
				  .MapToSingle()
				  .WrapInterface<ChannelCreated, IChannelCreateResponse>();
		}

		public override async Task<R<ServerGroupAddResponse, CommandError>> ServerGroupAdd(string name, GroupType? type = null)
		{
			var result = await SendNotifyCommand(new TsCommand("servergroupadd") {
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
				.MapToSingle();
		}

		public override async Task<R<FileUpload, CommandError>> FileTransferInitUpload(ChannelId channelId, string path,
			string channelPassword, ushort clientTransferId, long fileSize, bool overwrite, bool resume)
		{
			var result = await SendNotifyCommand(new TsCommand("ftinitupload") {
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
				return result.MapToSingle<FileUpload>();
			else
			{
				var ftresult = result.MapToSingle<FileTransferStatus>();
				if (!ftresult)
					return ftresult.Error;
				return new CommandError() { Id = ftresult.Value.Status, Message = ftresult.Value.Message };
			}
		}

		public override async Task<R<FileDownload, CommandError>> FileTransferInitDownload(ChannelId channelId,
			string path, string channelPassword, ushort clientTransferId, long seek)
		{
			var result = await SendNotifyCommand(new TsCommand("ftinitdownload") {
				{ "cid", channelId },
				{ "name", path },
				{ "cpw", channelPassword },
				{ "clientftfid", clientTransferId },
				{ "seekpos", seek } }, NotificationType.FileDownload, NotificationType.FileTransferStatus);
			if (!result.Ok)
				return result.Error;
			if (result.Value.NotifyType == NotificationType.FileDownload)
				return result.MapToSingle<FileDownload>();
			else
			{
				var ftresult = result.MapToSingle<FileTransferStatus>();
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
		public Reason? ExitReason { get; set; }
		public TsCrypt TsCrypt { get; }
		public PacketHandler<S2C, C2S> PacketHandler { get; set; }
		public ConnectionDataFull ConnectionDataFull { get; set; }

		public TaskCompletionSource<E<CommandError>> ConnectEvent { get; }
		public TaskCompletionSource<object?> DisconnectEvent { get; }

		public ConnectionContext(ConnectionDataFull connectionDataFull)
		{
			// Note: TCS.SetResult can continue to run the code of the 'await TSC.Task'
			// somewhere else synchronously.
			// While the TsFullClient class is designed to be resistend to problems regarding
			// intermediate state changes with such call, we still add the runasync Task
			// option for a more consistent processing order and better predictable behaviour.
			ConnectEvent = new TaskCompletionSource<E<CommandError>>(TaskCreationOptions.RunContinuationsAsynchronously);
			DisconnectEvent = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			TsCrypt = new TsCrypt(connectionDataFull.Identity);
			PacketHandler = new PacketHandler<S2C, C2S>(TsCrypt, connectionDataFull.LogId);
			ConnectionDataFull = connectionDataFull;
		}
	}
}
