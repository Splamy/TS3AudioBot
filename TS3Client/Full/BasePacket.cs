using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3Client.Full
{
	class BasePacket
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
		public int Size => Data.Length;

		public byte[] Raw { get; set; }
		public byte[] Header { get; protected set; }
		public byte[] Data { get; set; }

		public BasePacket()
		{
		}


		public bool FragmentedFlag
		{
			get { return (PacketFlags.HasFlag(PacketFlags.Fragmented)); }
			set
			{
				if (value) PacketTypeFlagged |= (byte)PacketFlags.Fragmented;
				else PacketTypeFlagged &= (byte)~PacketFlags.Fragmented;
			}
		}
		public bool NewProtocolFlag
		{
			get { return (PacketFlags.HasFlag(PacketFlags.Newprotocol)); }
			set
			{
				if (value) PacketTypeFlagged |= (byte)PacketFlags.Newprotocol;
				else PacketTypeFlagged &= (byte)~PacketFlags.Newprotocol;
			}
		}
		public bool CompressedFlag
		{
			get { return (PacketFlags.HasFlag(PacketFlags.Compressed)); }
			set
			{
				if (value) PacketTypeFlagged |= (byte)PacketFlags.Compressed;
				else PacketTypeFlagged &= (byte)~PacketFlags.Compressed;
			}
		}
		public bool UnencryptedFlag
		{
			get { return (PacketFlags.HasFlag(PacketFlags.Unencrypted)); }
			set
			{
				if (value) PacketTypeFlagged |= (byte)PacketFlags.Unencrypted;
				else PacketTypeFlagged &= (byte)~PacketFlags.Unencrypted;
			}
		}
	}
}
