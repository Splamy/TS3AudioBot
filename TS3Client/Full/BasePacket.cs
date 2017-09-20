// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Full
{
	public class BasePacket
	{
		public PacketType PacketType
		{
			get => (PacketType)(PacketTypeFlagged & 0x0F);
			set => PacketTypeFlagged = (byte)((PacketTypeFlagged & 0xF0) | ((byte)value & 0x0F));
		}
		public PacketFlags PacketFlags
		{
			get => (PacketFlags)(PacketTypeFlagged & 0xF0);
			set => PacketTypeFlagged = (byte)((PacketTypeFlagged & 0x0F) | ((byte)value & 0xF0));
		}
		public byte PacketTypeFlagged { get; set; }
		public ushort PacketId { get; set; }
		public uint GenerationId { get; set; }
		public int Size => Data.Length;

		public byte[] Raw { get; set; }
		public byte[] Header { get; protected set; }
		public byte[] Data { get; set; }

		public bool FragmentedFlag
		{
			get => (PacketFlags & PacketFlags.Fragmented) != 0;
			set
			{
				if (value) PacketTypeFlagged |= (byte)PacketFlags.Fragmented;
				else PacketTypeFlagged &= (byte)~PacketFlags.Fragmented;
			}
		}
		public bool NewProtocolFlag
		{
			get => (PacketFlags & PacketFlags.Newprotocol) != 0;
			set
			{
				if (value) PacketTypeFlagged |= (byte)PacketFlags.Newprotocol;
				else PacketTypeFlagged &= (byte)~PacketFlags.Newprotocol;
			}
		}
		public bool CompressedFlag
		{
			get => (PacketFlags & PacketFlags.Compressed) != 0;
			set
			{
				if (value) PacketTypeFlagged |= (byte)PacketFlags.Compressed;
				else PacketTypeFlagged &= (byte)~PacketFlags.Compressed;
			}
		}
		public bool UnencryptedFlag
		{
			get => (PacketFlags & PacketFlags.Unencrypted) != 0;
			set
			{
				if (value) PacketTypeFlagged |= (byte)PacketFlags.Unencrypted;
				else PacketTypeFlagged &= (byte)~PacketFlags.Unencrypted;
			}
		}

		public override string ToString()
		{
			return $"Type: {PacketType}\tFlags: [ " +
				$"{(FragmentedFlag ? "X" : "_")} {(NewProtocolFlag ? "X" : "_")} " +
				$"{(CompressedFlag ? "X" : "_")} {(UnencryptedFlag ? "X" : "_")} ]\t" +
				$"Id: {PacketId}\n" +
				$"  Data: { DebugUtil.DebugToHex(Data) }\n" +
				$"  ASCI: { Util.Encoder.GetString(Data) }";
		}
	}
}
