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

	/// <summary>Used to pass basic connecting information. (Usually for the query)</summary>
	public class ConnectionData
	{
		/// <summary>Hostname or Ip-Address including the port. This address can point to the
		/// server or the tsdns service.</summary>
		public string Address { get; set; }
	}

	/// <summary>Used to pass detailed connecting information to the full client.</summary>
	public class ConnectionDataFull : ConnectionData
	{
		/// <summary>
		/// Secret identity of the user.
		/// </summary>
		public IdentityData Identity { get; set; }
		/// <summary>
		/// Set this to the TeamSpeak 3 Version this client should appear as.
		/// You can find predefined version data in the <see cref="Full.VersionSign"/>
		/// class. Please keep in mind that the version data has to have valid sign
		/// to be accepted by an official TeamSpeak 3 Server.
		/// </summary>
		public VersionSign VersionSign { get; set; } = VersionSign.VER_WIN_3_1_8;
		/// <summary>The display username.</summary>
		public string Username { get; set; }
		/// <summary>The server password. Leave null if none.</summary>
		public Password ServerPassword { get; set; } = new Password();
		/// <summary>
		/// <para>The default channel this client should try to join when connecting.</para>
		/// <para>The channel can be specified with either the channel name path, example: "Lobby/Home".
		/// Or with the channel id in the following format: /&lt;id&gt;, example: "/5"</para>
		/// </summary>
		public string DefaultChannel { get; set; } = string.Empty;
		/// <summary>Password for the default channel. Leave null if none.</summary>
		public Password DefaultChannelPassword { get; set; } = new Password();
	}

	public class Password
	{
		private string hashedPassword;
		private string plainPassword;
		/// <summary>
		/// This can be set to true, when the password is already hashed.
		/// The hash works like this: base64(sha1(password))
		/// </summary>
		private bool isHashed;
		public string HashedPassword
		{
			get
			{
				if (isHashed && hashedPassword == null)
					return string.Empty;
				if (!isHashed)
					HashedPassword = Ts3Crypt.HashPassword(plainPassword);
				return hashedPassword;
			}
			set
			{
				hashedPassword = value;
				plainPassword = null;
				isHashed = true;
			}
		}
		public string PlainPassword { set { plainPassword = value; isHashed = false; } }

		public Password() { }
		public static Password FromHash(string hash) => new Password() { HashedPassword = hash };
		public static Password FromPlain(string pass) => new Password() { PlainPassword = pass };

		public static implicit operator Password(string pass) => FromPlain(pass);
	}
}
