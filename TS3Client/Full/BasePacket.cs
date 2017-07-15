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
	public class BasePacket
	{
		public PacketType PacketType
		{
			get { return (PacketType)(PacketTypeFlagged & 0x0F); }
			set { PacketTypeFlagged = (byte)((PacketTypeFlagged & 0xF0) | ((byte)value & 0x0F)); }
		}
		public PacketFlags PacketFlags
		{
			get { return (PacketFlags)(PacketTypeFlagged & 0xF0); }
			set { PacketTypeFlagged = (byte)((PacketTypeFlagged & 0x0F) | ((byte)value & 0xF0)); }
		}
		public byte PacketTypeFlagged { get; set; }
		public ushort PacketId { get; set; }
		public uint GenerationId { get; set; }
		public int Size => Data.Length;

		public byte[] Raw { get; set; }
		public byte[] Header { get; protected set; }
		public byte[] Data { get; set; }

		public BasePacket()
		{
		}

		public bool FragmentedFlag
		{
			get { return ((PacketFlags & PacketFlags.Fragmented) != 0); }
			set
			{
				if (value) PacketTypeFlagged |= (byte)PacketFlags.Fragmented;
				else PacketTypeFlagged &= (byte)~PacketFlags.Fragmented;
			}
		}
		public bool NewProtocolFlag
		{
			get { return ((PacketFlags & PacketFlags.Newprotocol) != 0); }
			set
			{
				if (value) PacketTypeFlagged |= (byte)PacketFlags.Newprotocol;
				else PacketTypeFlagged &= (byte)~PacketFlags.Newprotocol;
			}
		}
		public bool CompressedFlag
		{
			get { return ((PacketFlags & PacketFlags.Compressed) != 0); }
			set
			{
				if (value) PacketTypeFlagged |= (byte)PacketFlags.Compressed;
				else PacketTypeFlagged &= (byte)~PacketFlags.Compressed;
			}
		}
		public bool UnencryptedFlag
		{
			get { return ((PacketFlags & PacketFlags.Unencrypted) != 0); }
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
