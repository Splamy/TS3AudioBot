namespace TS3AudioBot.Helper.AudioTags
{
	using System.IO;
	using System.Runtime.InteropServices;

	static class BinaryReaderBigEndianExtensions
	{
		public static short ReadInt16BE(this BinaryReader br)
		{
			return BitConverterBigEndian.ToInt16(br.ReadBytes(sizeof(short)));
		}
		public static int ReadInt24BE(this BinaryReader br)
		{
			return BitConverterBigEndian.ToInt24(br.ReadBytes(3));
		}
		public static int ReadInt32BE(this BinaryReader br)
		{
			return BitConverterBigEndian.ToInt32(br.ReadBytes(sizeof(int)));
		}
		public static long ReadInt64BE(this BinaryReader br)
		{
			return BitConverterBigEndian.ToInt64(br.ReadBytes(sizeof(long)));
		}

		public static ushort ReadUInt16BE(this BinaryReader br)
		{
			return BitConverterBigEndian.ToUInt16(br.ReadBytes(sizeof(ushort)));
		}
		public static uint ReadUInt32BE(this BinaryReader br)
		{
			return BitConverterBigEndian.ToUInt32(br.ReadBytes(sizeof(uint)));
		}
		public static ulong ReadUInt64BE(this BinaryReader br)
		{
			return BitConverterBigEndian.ToUInt64(br.ReadBytes(sizeof(ulong)));
		}
	}

	class BitConverterBigEndian
	{
		private const int BITS_IN_BYTE = 8;

		private BitConverterBigEndian() { }

		public static short ToInt16(byte[] bytes)
		{
			return (short)(
				(bytes[0] << 1 * BITS_IN_BYTE) |
				(bytes[1] << 0 * BITS_IN_BYTE));
		}
		public static int ToInt24(byte[] bytes)
		{
			return (int)(
				(bytes[0] << 2 * BITS_IN_BYTE) |
				(bytes[1] << 1 * BITS_IN_BYTE) |
				(bytes[2] << 0 * BITS_IN_BYTE));
		}
		public static int ToInt32(byte[] bytes)
		{
			return (int)(
				(bytes[0] << 3 * BITS_IN_BYTE) |
				(bytes[1] << 2 * BITS_IN_BYTE) |
				(bytes[2] << 1 * BITS_IN_BYTE) |
				(bytes[3] << 0 * BITS_IN_BYTE));
		}
		public static long ToInt64(byte[] bytes)
		{
			ReinterpretInt ri;
			ri.value = 0;
			ri.HDW = // High double word
				(bytes[0] << 3 * BITS_IN_BYTE) |
				(bytes[1] << 2 * BITS_IN_BYTE) |
				(bytes[2] << 1 * BITS_IN_BYTE) |
				(bytes[3] << 0 * BITS_IN_BYTE);
			ri.LDW = // Low double word
				(bytes[4] << 3 * BITS_IN_BYTE) |
				(bytes[5] << 2 * BITS_IN_BYTE) |
				(bytes[6] << 1 * BITS_IN_BYTE) |
				(bytes[7] << 0 * BITS_IN_BYTE);
			return (long)ri.value;
		}

		public static ushort ToUInt16(byte[] bytes)
		{
			return (ushort)(
				(bytes[0] << 1 * BITS_IN_BYTE) |
				(bytes[1] << 0 * BITS_IN_BYTE));
		}
		public static uint ToUInt32(byte[] bytes)
		{
			return (uint)(
				(bytes[0] << 3 * BITS_IN_BYTE) |
				(bytes[1] << 2 * BITS_IN_BYTE) |
				(bytes[2] << 1 * BITS_IN_BYTE) |
				(bytes[3] << 0 * BITS_IN_BYTE));
		}
		public static ulong ToUInt64(byte[] bytes)
		{
			ReinterpretInt ri;
			ri.value = 0;
			ri.HDW = // High double word
				(bytes[0] << 3 * BITS_IN_BYTE) |
				(bytes[1] << 2 * BITS_IN_BYTE) |
				(bytes[2] << 1 * BITS_IN_BYTE) |
				(bytes[3] << 0 * BITS_IN_BYTE);
			ri.LDW = // Low double word
				(bytes[4] << 3 * BITS_IN_BYTE) |
				(bytes[5] << 2 * BITS_IN_BYTE) |
				(bytes[6] << 1 * BITS_IN_BYTE) |
				(bytes[7] << 0 * BITS_IN_BYTE);
			return (ulong)ri.value;
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct ReinterpretInt
		{
			[FieldOffset(0)]
			public int LDW;
			[FieldOffset(4)]
			public int HDW;
			[FieldOffset(0)]
			public long value;
		}
	}
}
