namespace TS3Client.Full
{
	using System;

	class OutgoingPacket : BasePacket
	{
		public ushort ClientId { get; set; }

		public DateTime LastSendTime { get; set; } = DateTime.MinValue;

		public OutgoingPacket(byte[] data, PacketType type)
		{
			Data = data;
			PacketType = type;
			Header = new byte[5];
		}

		public void BuildHeader()
		{
			if (!BitConverter.IsLittleEndian)
			{
				Header[0] = (byte)((ClientId >> 8) & 0xFF);
				Header[1] = (byte)(ClientId & 0xFF);
				Header[2] = (byte)((PacketId >> 8) & 0xFF);
				Header[3] = (byte)(PacketId & 0xFF);
			}
			else
			{
				Header[0] = (byte)(ClientId & 0xFF);
				Header[1] = (byte)((ClientId >> 8) & 0xFF);
				Header[2] = (byte)(PacketId & 0xFF);
				Header[3] = (byte)((PacketId >> 8) & 0xFF);
			}
			Header[4] = PacketTypeFlagged;
		}
	}
}