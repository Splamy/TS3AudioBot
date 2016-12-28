namespace TS3Client.Full
{
	using System;

	class OutgoingPacket : BasePacket
	{
		public ushort ClientId { get; set; }

		public DateTime LastSendTime { get; set; } = DateTime.MaxValue;
		public int ResendCount { get; set; } = 0;

		public OutgoingPacket(byte[] data, PacketType type)
		{
			Data = data;
			PacketType = type;
			Header = new byte[5];
		}

		public void BuildHeader()
		{
			NetUtil.H2N(PacketId, Header, 0);
			NetUtil.H2N(ClientId, Header, 2);
			Header[4] = PacketTypeFlagged;
		}
	}
}