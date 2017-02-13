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
	using System;
	using System.Net;

	internal static class NetUtil
	{
		// Common Network to Host and Host to Network swaps
		public static uint H2N(uint value) => unchecked((uint)IPAddress.HostToNetworkOrder((int)value));

		public static ushort N2Hushort(byte[] intArr, int inOff)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (ushort)(intArr[inOff] | (intArr[inOff + 1] << 8));
			}
			else
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
			else
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
			else
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
			else
			{
				return (intArr[inOff] << 24) | (intArr[inOff + 1] << 16) | (intArr[inOff + 2] << 8) | intArr[inOff + 3];
			}
		}
	}
}
