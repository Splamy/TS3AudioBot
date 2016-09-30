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
	}
}
