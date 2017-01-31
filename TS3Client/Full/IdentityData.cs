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
	using Org.BouncyCastle.Math;
	using Org.BouncyCastle.Math.EC;

	public class IdentityData
	{
		public string PublicKeyString { get; set; }
		public string PrivateKeyString { get; set; }
		public ECPoint PublicKey { get; set; }
		public BigInteger PrivateKey { get; set; }
		public ulong ValidKeyOffset { get; set; }
		public ulong LastCheckedKeyOffset { get; set; }
	}
}
