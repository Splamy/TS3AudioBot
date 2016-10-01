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
		public static int H2N(int value) => IPAddress.HostToNetworkOrder(value);
		public static int N2H(int value) => IPAddress.NetworkToHostOrder(value);
		public static ulong H2N(ulong value) => unchecked((ulong)IPAddress.HostToNetworkOrder((long)value));
		public static ulong N2H(ulong value) => unchecked((ulong)IPAddress.NetworkToHostOrder((long)value));


		public static ushort N2Hushort(byte[] intArr, int inOff)
		{
			if (BitConverter.IsLittleEndian)
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
			if (BitConverter.IsLittleEndian)
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
	}
}
