// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client
{
	using Full;

	public class ConnectionData
	{
		/// <summary>Hostname or Ip-Address including the port. This address can point to the
		/// server or the tsdns service.</summary>
		public string Address { get; set; }
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
		/// <summary>
		/// <para>The display username.</para>
		/// </summary>
		public string Username { get; set; }
		/// <summary>
		/// <para>The server password. Leave null if none.</para>
		/// </summary>
		public string Password { get; set; }
		/// <summary>
		/// <para>The default channel this client should try to join when connecting.</para>
		/// <para>The channel can be specified with either the channel name path, example: "Lobby/Home".
		/// Or with the channel id in the following format: /&lt;id&gt;, example: "/5"</para>
		/// </summary>
		public string DefaultChannel { get; set; } = string.Empty;
	}
}
