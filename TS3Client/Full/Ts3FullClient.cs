namespace TS3Client.Full
{
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net;
	using System.Net.Sockets;

	public sealed class Ts3FullClient : Ts3BaseClient
	{
		private readonly UdpClient udpClient;
		private readonly Ts3Crypt ts3Crypt;
		private readonly PacketHandler packetHandler;

		private int returnCode;

		public override ClientType ClientType => ClientType.Full;
		public ushort ClientId => packetHandler.ClientId;

		public Ts3FullClient(EventDispatchType dispatcher) : base(dispatcher)
		{
			udpClient = new UdpClient();
			ts3Crypt = new Ts3Crypt();
			packetHandler = new PacketHandler(ts3Crypt, udpClient);

			returnCode = 0;
		}

		protected override void ConnectInternal(ConnectionData conData)
		{
			var conDataFull = conData as ConnectionDataFull;
			if (conDataFull == null)
				throw new ArgumentException($"Use the {nameof(ConnectionDataFull)} deriverate to connect with the full client.", nameof(conData));
			if (conDataFull.Identity == null)
				throw new ArgumentNullException(nameof(conData));

			Reset();

			packetHandler.Start();

			try
			{
				var hostEntry = Dns.GetHostEntry(conData.Hostname);
				var ipAddr = hostEntry.AddressList.FirstOrDefault();
				if (ipAddr == null) throw new Ts3Exception("Could not resove DNS.");
				packetHandler.RemoteAddress = new IPEndPoint(ipAddr, conData.Port);
				udpClient.Connect(packetHandler.RemoteAddress);
			}
			catch (SocketException ex) { throw new Ts3Exception("Could not connect", ex); }

			ts3Crypt.Identity = conDataFull.Identity;
			var initData = ts3Crypt.ProcessInit1(null);
			packetHandler.AddOutgoingPacket(initData, PacketType.Init1);
		}

		protected override void DisconnectInternal()
		{
			ClientDisconnect(MoveReason.LeftServer, "Disconnected");
			//udpClient.Close();
		}

		protected override void NetworkLoop()
		{
			while (true)
			{
				var packet = packetHandler.FetchPacket();
				if (packet == null) break;

				switch (packet.PacketType)
				{
					case PacketType.Command:
						string message = Util.Encoder.GetString(packet.Data, 0, packet.Data.Length);
						if (!SpecialCommandProcess(message))
							ProcessCommand(message);
						break;

					case PacketType.Readable:
					case PacketType.Voice:
						// VOICE

						break;

					case PacketType.Init1:
						var forwardData = ts3Crypt.ProcessInit1(packet.Data);
						packetHandler.AddOutgoingPacket(forwardData, PacketType.Init1);
						break;
				}
			}
			Status = Ts3ClientStatus.Disconnected;
		}

		private bool SpecialCommandProcess(string message)
		{
			if (message.StartsWith("initivexpand ", StringComparison.Ordinal)
				|| message.StartsWith("initserver ", StringComparison.Ordinal)
				|| message.StartsWith("channellist ", StringComparison.Ordinal)
				|| message.StartsWith("channellistfinished ", StringComparison.Ordinal))
			{
				var notification = CommandDeserializer.GenerateNotification(message);
				InvokeEvent(notification.Item1, notification.Item2);
				return true;
			}
			return false;
		}

		protected override void ProcessInitIvExpand(InitIvExpand initIvExpand)
		{
			ts3Crypt.CryptoInit(initIvExpand.Alpha, initIvExpand.Beta, initIvExpand.Omega);
			packetHandler.CryptoInitDone();
			ClientInit(
				ConnectionData.Username,
				"Windows",
				true, true,
				string.Empty, string.Empty,
				ConnectionData.Password,
				string.Empty, string.Empty, string.Empty, "123,456",
				VersionSign.VER_3_0_19_03);
		}

		protected override void ProcessInitServer(InitServer initServer)
		{
			packetHandler.ClientId = initServer.ClientId;
			ConnectDone();
		}

		protected override IEnumerable<IResponse> SendCommand(Ts3Command com, Type targetType)
		{
			if (com.ExpectResponse)
				com.AppendParameter(new CommandParameter("return_code", returnCode));

			using (WaitBlock wb = new WaitBlock(targetType))
			{
				lock (LockObj)
				{
					if (com.ExpectResponse)
					{
						RequestQueue.Enqueue(wb);
						returnCode++;
					}

					byte[] data = Util.Encoder.GetBytes(com.ToString());
					packetHandler.AddOutgoingPacket(data, PacketType.Command);
				}

				if (com.ExpectResponse)
					return wb.WaitForMessage();
				else
					return null;
			}
		}

		protected override void Reset()
		{
			base.Reset();

			ts3Crypt.Reset();
			packetHandler.Reset();

			returnCode = 0;
		}

		#region FULLCLIENT SPECIFIC COMMANDS

		public void ClientInit(string nickname, string plattform, bool inputHardware, bool outputHardware,
				string defaultChannel, string defaultChannelPassword, string serverPassword, string metaData,
				string nicknamePhonetic, string defaultToken, string hwid, VersionSign versionSign)
			=> SendNoResponsed(
				new Ts3Command("clientinit", new List<CommandParameter>() {
					new CommandParameter("client_nickname", nickname),
					new CommandParameter("client_version", versionSign.Name),
					new CommandParameter("client_platform", plattform),
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
			=> Send("clientdisconnect",
				new CommandParameter("reasonid", (int)reason),
				new CommandParameter("reasonmsg", reasonMsg));

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

			packetHandler.AddOutgoingPacket(buffer, PacketType.Readable);
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

			packetHandler.AddOutgoingPacket(buffer, PacketType.Voice);
		}
		#endregion
	}
}
