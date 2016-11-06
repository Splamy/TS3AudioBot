using System;

namespace TS3Client.Full
{
	using System.Net;

	internal static class NetUtil
	{
		// Common Network to Host and Host to Network swaps
		public static ushort H2N(ushort value) => unchecked((ushort)IPAddress.HostToNetworkOrder((short)value));
		public static ushort N2H(ushort value) => unchecked((ushort)IPAddress.NetworkToHostOrder((short)value));
		public static uint H2N(uint value) => unchecked((uint)IPAddress.HostToNetworkOrder((int)value));
		public static uint N2H(uint value) => unchecked((uint)IPAddress.NetworkToHostOrder((int)value));


		public static ushort N2Hushort(byte[] intArr, int inOff)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (ushort)(intArr[inOff] | (intArr[inOff + 1] << 8));
			}
			else // IsBigEndian
			{
				return (ushort)((intArr[inOff] << 8) | intArr[inOff + 1]);
			}
		}
		public static void H2N(ushort value, byte[] outArr, int outOff)
		{
			if (!BitConverter.IsLittleEndian)
			{
				outArr[outOff] = (byte)(value & 0xFF);
				outArr[outOff + 1] = (byte)((value >> 8) & 0xFF);
			}
			else // IsBigEndian
			{
				outArr[outOff] = (byte)((value >> 8) & 0xFF);
				outArr[outOff + 1] = (byte)(value & 0xFF);
			}
		}

		public static void H2N(ulong value, byte[] outArr, int outOff)
		{
			if (!BitConverter.IsLittleEndian)
			{
				outArr[outOff + 0] = (byte)((value >> 00) & 0xFF);
				outArr[outOff + 1] = (byte)((value >> 08) & 0xFF);
				outArr[outOff + 2] = (byte)((value >> 16) & 0xFF);
				outArr[outOff + 3] = (byte)((value >> 24) & 0xFF);
				outArr[outOff + 4] = (byte)((value >> 32) & 0xFF);
				outArr[outOff + 5] = (byte)((value >> 40) & 0xFF);
				outArr[outOff + 6] = (byte)((value >> 48) & 0xFF);
				outArr[outOff + 7] = (byte)((value >> 56) & 0xFF);
			}
			else // IsBigEndian
			{
				outArr[outOff + 0] = (byte)((value >> 56) & 0xFF);
				outArr[outOff + 1] = (byte)((value >> 48) & 0xFF);
				outArr[outOff + 2] = (byte)((value >> 40) & 0xFF);
				outArr[outOff + 3] = (byte)((value >> 32) & 0xFF);
				outArr[outOff + 4] = (byte)((value >> 24) & 0xFF);
				outArr[outOff + 5] = (byte)((value >> 16) & 0xFF);
				outArr[outOff + 6] = (byte)((value >> 08) & 0xFF);
				outArr[outOff + 7] = (byte)((value >> 00) & 0xFF);
			}
		}

		public static int N2Hint(byte[] intArr, int inOff)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return intArr[inOff] | (intArr[inOff + 1] << 8) | (intArr[inOff + 2] << 16) | (intArr[inOff + 3] << 24);
			}
			else // IsBigEndian
			{
				return (intArr[inOff] << 24) | (intArr[inOff + 1] << 16) | (intArr[inOff + 2] << 8) | intArr[inOff + 3];
			}
		}
	}
}
