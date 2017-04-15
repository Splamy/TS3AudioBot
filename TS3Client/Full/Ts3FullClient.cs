// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3Client.Full
{
	using Commands;
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;

	public sealed class Ts3FullClient : Ts3BaseFunctions
	{
		private readonly Ts3Crypt ts3Crypt;
		private readonly PacketHandler packetHandler;
		private readonly MessageProcessor msgProc;

		private readonly object CommmandQueueLock = new object();
		private readonly object StatusLock = new object();

		private int returnCode;
		private bool wasExit;

		private IEventDispatcher dispatcher;
		public override ClientType ClientType => ClientType.Full;
		public ushort ClientId => packetHandler.ClientId;
		public string QuitMessage { get; set; } = "Disconnected";
		public VersionSign VersionSign { get; private set; }
		private Ts3ClientStatus Status;
		public override bool Connected => Status == Ts3ClientStatus.Connected;
		private ConnectionDataFull connectionDataFull;

		public override event NotifyEventHandler<TextMessage> OnTextMessageReceived;
		public override event NotifyEventHandler<ClientEnterView> OnClientEnterView;
		public override event NotifyEventHandler<ClientLeftView> OnClientLeftView;
		public override event EventHandler<EventArgs> OnConnected;
		public override event EventHandler<DisconnectEventArgs> OnDisconnected;
		public event EventHandler<CommandError> OnErrorEvent;

		public Ts3FullClient(EventDispatchType dispatcherType)
		{
			Status = Ts3ClientStatus.Disconnected;
			ts3Crypt = new Ts3Crypt();
			packetHandler = new PacketHandler(ts3Crypt);
			msgProc = new MessageProcessor(false);
			dispatcher = EventDispatcherHelper.Create(dispatcherType);
			wasExit = true;
		}

		public override void Connect(ConnectionData conData)
		{
			var conDataFull = conData as ConnectionDataFull;
			if (conDataFull == null) throw new ArgumentException($"Use the {nameof(ConnectionDataFull)} deriverate to connect with the full client.", nameof(conData));
			if (conDataFull.Identity == null) throw new ArgumentNullException(nameof(conDataFull.Identity));
			if (conDataFull.VersionSign == null) throw new ArgumentNullException(nameof(conDataFull.VersionSign));
			connectionDataFull = conDataFull;

			Disconnect();

			lock (StatusLock)
			{
				returnCode = 0;
				wasExit = false;

				VersionSign = conDataFull.VersionSign;
				ts3Crypt.Identity = conDataFull.Identity;

				packetHandler.Connect(conData.Hostname, conData.Port);
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

		private void DisconnectInternal(bool manualLock = false, bool triggerEvent = true)
		{
			if (wasExit)
				return;

			if (!manualLock)
				Monitor.Enter(StatusLock);

			try
			{
				switch (Status)
				{
				case Ts3ClientStatus.Disconnected:
					if (!wasExit)
					{
						wasExit = true;
						packetHandler.Stop();
						msgProc.DropQueue();
						dispatcher.Dispose();
						if (triggerEvent)
							OnDisconnected?.Invoke(this, new DisconnectEventArgs(packetHandler.ExitReason ?? MoveReason.LeftServer));
					}
					break;
				case Ts3ClientStatus.Disconnecting:
					break;
				case Ts3ClientStatus.Connected:
					ClientDisconnect(MoveReason.LeftServer, QuitMessage);
					Status = Ts3ClientStatus.Disconnecting;
					break;
				case Ts3ClientStatus.Connecting:
					break;
				default:
					break;
				}
			}
			finally
			{
				if (!manualLock)
					Monitor.Exit(StatusLock);
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
					lock (StatusLock)
					{
						Status = Ts3ClientStatus.Disconnected;
						DisconnectInternal(true);
					}
					break;
				}
				OnClientLeftView?.Invoke(this, clientLeftArr);
				break;

			case NotificationType.ClientMoved: break;
			case NotificationType.ServerEdited: break;
			case NotificationType.TextMessage: OnTextMessageReceived?.Invoke(this, notification.Cast<TextMessage>()); break;
			case NotificationType.TokenUsed: break;
			// full client events
			case NotificationType.InitIvExpand: ProcessInitIvExpand((InitIvExpand)notification.FirstOrDefault()); break;
			case NotificationType.InitServer: ProcessInitServer((InitServer)notification.FirstOrDefault()); break;
			case NotificationType.ChannelList: break;
			case NotificationType.ChannelListFinished: break;
			case NotificationType.ClientNeededPermissions: break;
			case NotificationType.ClientChannelGroupChanged: break;
			case NotificationType.ClientServerGroupAdded: break;
			case NotificationType.ConnectionInfoRequest: ProcessConnectionInfoRequest((ConnectionInfoRequest)notification.FirstOrDefault()); break;
			case NotificationType.ChannelSubscribed: break;
			case NotificationType.ChannelUnsubscribed: break;
			case NotificationType.ClientChatComposing: break;
			// special
			case NotificationType.Error:
				lock (StatusLock)
				{
					if (Status == Ts3ClientStatus.Connecting)
					{
						Status = Ts3ClientStatus.Disconnected;
						DisconnectInternal(true, false);
					}
				}

				OnErrorEvent?.Invoke(this, (CommandError)notification.First());
				break;
			case NotificationType.Unknown:
			default: throw new InvalidOperationException();
			}
		}

		private void NetworkLoop()
		{
			while (true)
			{
				lock (StatusLock)
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

			lock (StatusLock)
			{
				Status = Ts3ClientStatus.Disconnected;
				DisconnectInternal(true);
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
				string.Empty, string.Empty, password,
				string.Empty, string.Empty, string.Empty, "123,456",
				VersionSign);
		}

		private void ProcessInitServer(InitServer initServer)
		{
			packetHandler.ClientId = initServer.ClientId;
			packetHandler.ReceiveInitAck();

			// CP
			Status = Ts3ClientStatus.Connected;
			OnConnected?.Invoke(this, new EventArgs());
			// CP
		}

		private void ProcessConnectionInfoRequest(ConnectionInfoRequest conInfoRequest)
		{
			SendNoResponsed(packetHandler.NetworkStats.GenerateStatusAnswer());
		}

		protected override IEnumerable<T> SendCommand<T>(Ts3Command com)
		{
			var retCode = new CommandParameter("return_code", returnCode);
			if (com.ExpectResponse)
				com.AppendParameter(retCode);

			using (var wb = new WaitBlock())
			{
				lock (CommmandQueueLock)
				{
					if (com.ExpectResponse)
					{
						msgProc.EnqueueRequest(retCode.Value, wb);
						returnCode++;
					}

					byte[] data = Util.Encoder.GetBytes(com.ToString());
					lock (StatusLock)
					{
						if (wasExit)
							throw new Ts3CommandException(new CommandError { Id = Ts3ErrorCode.custom_error, Message = "Connection closed" });
						packetHandler.AddOutgoingPacket(data, PacketType.Command);
					}
				}

				if (com.ExpectResponse)
					return wb.WaitForMessage<T>();
				else
					return null;
			}
		}

		public override void Dispose()
		{
			Disconnect();
			dispatcher?.Dispose();
		}

		#region FULLCLIENT SPECIFIC COMMANDS

		public void ClientInit(string nickname, bool inputHardware, bool outputHardware,
				string defaultChannel, string defaultChannelPassword, string serverPassword, string metaData,
				string nicknamePhonetic, string defaultToken, string hwid, VersionSign versionSign)
			=> SendNoResponsed(
				new Ts3Command("clientinit", new List<CommandParameter>() {
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
				new Ts3Command("clientdisconnect", new List<CommandParameter>() {
					new CommandParameter("reasonid", (int)reason),
					new CommandParameter("reasonmsg", reasonMsg) }));

		public void SendAudio(byte[] buffer, int length, Codec codec)
		{
			// [X,X,Y,DATA]
			// > X is a ushort in H2N order of a own audio packet counter
			//     it seem it can be the same as the packet counter so we will let the packethandler do it.
			// > Y is the codec byte (see Enum)
			byte[] tmpBuffer = new byte[length + 3];
			tmpBuffer[2] = (byte)codec;
			Array.Copy(buffer, 0, tmpBuffer, 3, length);
			buffer = tmpBuffer;

			packetHandler.AddOutgoingPacket(buffer, PacketType.Voice);
		}

		public void SendAudioWhisper(byte[] buffer, int length, Codec codec, IList<ulong> channelIds, IList<ushort> clientIds)
		{
			// [X,X,Y,N,M,(U,U,U,U,U,U,U,U)*,(T,T)*,DATA]
			// > X is a ushort in H2N order of a own audio packet counter
			//     it seems it can be the same as the packet counter so we will let the packethandler do it.
			// > Y is the codec byte (see Enum)
			// > N is a byte, the count of ChannelIds to send to
			// > M is a byte, the count of ClientIds to send to
			// > U is a ulong in H2N order of each targeted channelId, U is repeated N times
			// > T is a ushort in H2N order of each targeted clientId, T is repeated M times
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
