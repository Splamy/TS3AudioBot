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
using System.Runtime.CompilerServices;
using TSLib.Helper;

namespace TSLib.Full
{
	internal struct Packet<TDir>
	{
		public static bool FromServer { get; } = typeof(TDir) == typeof(S2C);
		public static int HeaderLength { get; } = typeof(TDir) == typeof(S2C) ? S2C.HeaderLen : C2S.HeaderLen;

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

		public TDir HeaderExt { get; set; }

		public byte[] Raw { get; private set; }
		public byte[] Header { get; private set; }
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

		public Packet(ReadOnlySpan<byte> data, PacketType packetType, ushort packetId, uint generationId) : this()
		{
			Raw = new byte[data.Length + HeaderLength + TsCrypt.MacLen];
			Header = new byte[HeaderLength];
			Data = data.ToArray();
			PacketType = packetType;
			PacketId = packetId;
			GenerationId = generationId;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Packet<TDir>? FromRaw(ReadOnlySpan<byte> raw)
		{
			if (raw.Length < HeaderLength + TsCrypt.MacLen)
				return null;
			var packet = new Packet<TDir>
			{
				Raw = raw.ToArray(),
				Header = new byte[HeaderLength],
			};
			packet.FromHeader();
			return packet;
		}

		public override string ToString()
		{
			return $"Type: {PacketType}\tFlags: [ " +
				$"{(FragmentedFlag ? "F" : "_")} {(NewProtocolFlag ? "N" : "_")} " +
				$"{(CompressedFlag ? "C" : "_")} {(UnencryptedFlag ? "U" : "_")} ]\t" +
				$"Id: {PacketId}\n" +
				$"  MAC: { (Raw is null ? string.Empty : DebugUtil.DebugToHex(Raw.AsSpan(0, 8))) }\t" +
				$"  Header: { DebugUtil.DebugToHex(Header) }\n" +
				$"  Data: { DebugUtil.DebugToHex(Data) }";
		}

		public void BuildHeader() => BuildHeader(Header);
		public void BuildHeader(Span<byte> into)
		{
			// typeof(..) and casts get jitted away, don't worry :)
			if (typeof(TDir) == typeof(S2C))
			{
				BinaryPrimitives.WriteUInt16BigEndian(into.Slice(0, 2), PacketId);
				into[2] = PacketTypeFlagged;
			}
			else if (typeof(TDir) == typeof(C2S))
			{
				var self = (C2S)(object)HeaderExt;
				BinaryPrimitives.WriteUInt16BigEndian(into.Slice(0, 2), PacketId);
				BinaryPrimitives.WriteUInt16BigEndian(into.Slice(2, 2), self.ClientId);
				into[4] = PacketTypeFlagged;
			}
			else
			{
				throw new NotSupportedException();
			}
#if DEBUG
			into.CopyTo(Header.AsSpan());
#endif
		}

		public void FromHeader()
		{
			// typeof(..) and casts get jitted away, don't worry :)
			var rawSpan = Raw.AsSpan();
			if (typeof(TDir) == typeof(S2C))
			{
				PacketId = BinaryPrimitives.ReadUInt16BigEndian(rawSpan.Slice(TsCrypt.MacLen));
				PacketTypeFlagged = Raw[TsCrypt.MacLen + 2];
			}
			else if (typeof(TDir) == typeof(C2S))
			{
				var ext = new C2S();
				PacketId = BinaryPrimitives.ReadUInt16BigEndian(rawSpan.Slice(TsCrypt.MacLen));
				ext.ClientId = BinaryPrimitives.ReadUInt16BigEndian(rawSpan.Slice(TsCrypt.MacLen + 2));
				PacketTypeFlagged = Raw[TsCrypt.MacLen + 4];
				HeaderExt = (TDir)(object)ext;
			}
			else
			{
				throw new NotSupportedException();
			}
		}
	}

	internal class ResendPacket<T>
	{
		public /*readonly*/ Packet<T> Packet;
		public DateTime FirstSendTime { get; set; }
		public DateTime LastSendTime { get; set; }

		public ResendPacket(Packet<T> packet)
		{
			Packet = packet;
			var now = Tools.Now;
			FirstSendTime = now;
			LastSendTime = now;
		}

		public override string ToString() => $"RS(first:{FirstSendTime},last:{LastSendTime}) => {Packet}";
	}

	internal struct C2S
	{
		public const int HeaderLen = 5;

		public ushort ClientId { get; set; }
	}

	internal struct S2C
	{
		public const int HeaderLen = 3;
	}
}
