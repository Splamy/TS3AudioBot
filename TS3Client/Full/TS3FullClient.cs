namespace TS3Client.Full
{
	using System;
	using System.Collections.Generic;
	using System.Net.Sockets;
	using Messages;

	public sealed class TS3FullClient : TS3BaseClient
	{
		private readonly UdpClient udpClient;
		private readonly TS3Crypt ts3Crypt;
		private readonly PacketHandler packetHandler;

		private int returnCode;
		private readonly Dictionary<int, WaitBlock> requestDict;

		public TS3FullClient(EventDispatchType dispatcher) : base(dispatcher)
		{
			udpClient = new UdpClient();
			ts3Crypt = new TS3Crypt();
			packetHandler = new PacketHandler(ts3Crypt, udpClient);

			returnCode = 0;
			requestDict = new Dictionary<int, WaitBlock>();
		}

		protected override void ConnectInternal(ConnectionData conData)
		{
			Reset();

			try { udpClient.Connect(conData.Hostname, conData.Port); }
			catch (SocketException ex) { throw new TS3CommandException(new CommandError(), ex); }

			var initData = ts3Crypt.ProcessInit1(null);
			packetHandler.AddOutgoingPacket(initData, PacketType.Init1);
		}

		protected override void DisconnectInternal()
		{
			// TODO send quit message
			udpClient.Close();
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

						if (message.StartsWith("notify", StringComparison.Ordinal))
						{
							var notify = CommandDeserializer.GenerateNotification(message);
							InvokeEvent(notify);
							break;
						}

						if (message.StartsWith("error ", StringComparison.Ordinal))
						{
							var error = CommandDeserializer.GenerateErrorStatus(message);
							int errorReturnCode;
							WaitBlock requestBlock = null;
							if (int.TryParse(error.ReturnCode, out errorReturnCode))
							{
								lock (LockObj)
								{
									if (requestDict.TryGetValue(errorReturnCode, out requestBlock))
										requestDict.Remove(errorReturnCode);
								}
								requestBlock?.SetAnswer(error);
							}
							if (requestBlock == null)
							{
								UnrequestedAnswers(error);
							}
						}
						else
						{
							var error = CommandDeserializer.GenerateResponse(message);
							int errorReturnCode;
							WaitBlock requestBlock = null;
							if (int.TryParse(error.ReturnCode, out errorReturnCode))
							{
								lock (LockObj)
								{
									if (requestDict.TryGetValue(errorReturnCode, out requestBlock))
										requestDict.Remove(errorReturnCode);
								}
								requestBlock?.SetAnswer(error);
							}
							if (requestBlock == null)
							{
								UnrequestedAnswers(error);
							}
						}
						break;

					case PacketType.Readable:
						// VOICE

						break;

					case PacketType.Init1:
						var forwardData = ts3Crypt.ProcessInit1(packet.Data);
						packetHandler.AddOutgoingPacket(forwardData, PacketType.Init1);
						break;
				}
			}
			Status = TS3ClientStatus.Disconnected;
		}

		// temporary logic. It should be replaced by proper event handlers
		private void UnrequestedAnswers(object msg)
		{
			// objects here arent requested anyway so we dont need to process them
		}

		protected override IEnumerable<IResponse> SendCommand(TS3Command com, Type targetType)
		{
			com.AppendParameter(new CommandParameter("return_code", returnCode));

			using (WaitBlock wb = new WaitBlock(targetType))
			{
				lock (LockObj)
				{
					requestDict.Add(returnCode, wb);
					returnCode++;
				}

				byte[] data = Util.Encoder.GetBytes(com.ToString());
				packetHandler.AddOutgoingPacket(data, PacketType.Command);
				return wb.WaitForMessage();
			}
		}

		private void Reset()
		{
			ts3Crypt.Reset();
			packetHandler.Reset();

			returnCode = 0;
			requestDict.Clear();
		}

		#region FULLCLIENT SPECIFIC COMMANDS

		public void ClientInit(string nickname, string version, string plattform, bool inputHardware, bool outputHardware,
				string defaultChannel, string defaultChannelPassword, string serverPassword, string metaData,
				string versionSign, int keyOffset, string nicknamePhonetic, string defaultToken, string hwid)
			=> Send("clientinit",
			new CommandParameter("client_nickname", nickname),
			new CommandParameter("client_version", version),
			new CommandParameter("client_platform", plattform),
			new CommandParameter("client_input_hardware", inputHardware),
			new CommandParameter("client_output_hardware", outputHardware),
			new CommandParameter("client_default_channel", defaultChannel),
			new CommandParameter("client_default_channel_password", defaultChannelPassword),
			new CommandParameter("client_server_password", serverPassword),
			new CommandParameter("client_meta_data", metaData),
			new CommandParameter("client_version_sign", versionSign),
			new CommandParameter("client_key_offset", keyOffset),
			new CommandParameter("client_nickname_phonetic", nicknamePhonetic),
			new CommandParameter("client_default_token", defaultToken),
			new CommandParameter("hwid", hwid));

		#endregion
	}
}
