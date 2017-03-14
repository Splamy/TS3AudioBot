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

namespace TS3Client
{
	using Full;

	public class ConnectionData
	{
		/// <summary>Hostname or Ip-Address of the server.</summary>
		public string Hostname { get; set; }
		/// <summary>Teamspeak serer port. Usually 9987.</summary>
		public ushort Port { get; set; }
		/// <summary>
		/// <para>As Full Client: The display username.</para>
		/// <para>As Query Client: The query login name.</para>
		/// </summary>
		public string Username { get; set; }
		/// <summary>
		/// <para>As Full Client: The server password.</para>
		/// <para>As Query Client: The query login password.</para>
		/// </summary>
		public string Password { get; set; }
	}

	public class ConnectionDataFull : ConnectionData
	{
		/// <summary>
		/// Secret identity of the user.
		/// </summary>
		public IdentityData Identity { get; set; }
		/// <summary>
		/// This can be set to true, when the password is already hashed.
		/// The hash works like this: base64(sha1(password))
		/// </summary>
		public bool IsPasswordHashed { get; set; } = false;
		/// <summary>
		/// Set this to the TeamSpeak 3 Version this client should appear as.
		/// You can find predefined version data in the <see cref="Full.VersionSign"/>
		/// class. Please keep in mind that the version data has to have valid sign
		/// to be accepted by an official TeamSpeak 3 Server.
		/// </summary>
		public VersionSign VersionSign { get; set; } = VersionSign.VER_LIN_3_0_19_4;
	}
}
