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
	using Helper;
	using System;

	internal abstract class BasePacket
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
		public abstract bool FromServer { get; }
		public abstract int HeaderLength { get; }

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
				$"{(FragmentedFlag ? "F" : "_")} {(NewProtocolFlag ? "N" : "_")} " +
				$"{(CompressedFlag ? "C" : "_")} {(UnencryptedFlag ? "U" : "_")} ]\t" +
				$"Id: {PacketId}\n" +
				$"  MAC: { (Raw == null ? string.Empty : DebugUtil.DebugToHex(Raw.AsSpan().Slice(0, 8))) }\t" +
				$"  Header: { DebugUtil.DebugToHex(Header) }\n" +
				$"  Data: { DebugUtil.DebugToHex(Data) }";
		}

		public void BuildHeader() => BuildHeader(Header.AsSpan());
		public abstract void BuildHeader(Span<byte> into);
	}
}
