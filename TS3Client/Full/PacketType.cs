namespace TS3Client.Full
{
	using System;

	public enum PacketType : byte
	{
		Readable = 0x0,
		Voice = 0x1,
		Command = 0x2,
		CommandLow = 0x3,
		Ping = 0x4,
		Pong = 0x5,
		Ack = 0x6,
		AckLow = 0x7,
		Init1 = 0x8,
	}

	[Flags]
	public enum PacketFlags : byte
	{
        None = 0x0,
        Fragmented = 0x10,
		Newprotocol = 0x20,
		Compressed = 0x40,
		Unencrypted = 0x80,
	}
}
