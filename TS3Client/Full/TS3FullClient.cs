using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TS3Client.Messages;
using System.Net;
using System.Net.Sockets;

namespace TS3Client.Full
{
	public class TS3FullClient : TS3BaseClient
	{
		private readonly UdpClient udpClient;
		private readonly TS3Crypt ts3Crypt;
		private readonly PacketHandler packetHandler;

		public TS3FullClient(EventDispatchType dispatcher) : base(dispatcher)
		{
			udpClient = new UdpClient();
			ts3Crypt = new TS3Crypt();
			packetHandler = new PacketHandler(ts3Crypt, udpClient);
		}

		protected override void ConnectInternal(ConnectionData conData)
		{
			ts3Crypt.Reset();
			packetHandler.Reset();

			// TODO: connect
		}

		protected override void DisconnectInternal()
		{
			throw new NotImplementedException();
		}

		protected override void NetworkLoop()
		{
			while (true)
			{
				var packet = packetHandler.FetchPacket();

				
			}
		}

		protected override IEnumerable<IResponse> SendCommand(string data, Type targetType)
		{
			throw new NotImplementedException();
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
