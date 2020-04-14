// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using TSLib.Full;
using TSLib.Helper;

namespace TSLib
{
	/// <summary>Used to pass basic connecting information. (Usually for the query)</summary>
	public class ConnectionData
	{
		/// <summary>Hostname or Ip-Address including the port. This address can point to the
		/// server or the tsdns service.</summary>
		public string Address { get; }
		/// <summary>Attaches a name to log evets and threads. Useful for debugging.</summary>
		public Id LogId { get; }

		public ConnectionData(string address, Id? logId = null)
		{
			Address = address;
			LogId = logId ?? Id.Null;
		}
	}

	/// <summary>Used to pass detailed connecting information to the full client.</summary>
	public class ConnectionDataFull : ConnectionData
	{
		/// <summary>
		/// Secret identity of the user.
		/// </summary>
		public IdentityData Identity { get; }
		/// <summary>
		/// Set this to the TeamSpeak 3 Version this client should appear as.
		/// You can find predefined version data in the <see cref="TsVersionSigned"/>
		/// class. Please keep in mind that the version data has to have valid sign
		/// to be accepted by an official TeamSpeak 3 Server.
		/// </summary>
		public TsVersionSigned VersionSign { get; }
		/// <summary>The display username.</summary>
		public string Username { get; }
		/// <summary>The server password. Leave null if none.</summary>
		public Password ServerPassword { get; }
		/// <summary>
		/// <para>The default channel this client should try to join when connecting.</para>
		/// <para>The channel can be specified with either the channel name path, example: "Lobby/Home".
		/// Or with the channel id in the following format: /&lt;id&gt;, example: "/5"</para>
		/// </summary>
		public string DefaultChannel { get; }
		/// <summary>Password for the default channel. Leave null if none.</summary>
		public Password DefaultChannelPassword { get; }

		public ConnectionDataFull(
			string address,
			IdentityData identity,
			TsVersionSigned? versionSign = null,
			string? username = null,
			Password? serverPassword = null,
			string? defaultChannel = null,
			Password? defaultChannelPassword = null,
			Id? logId = null)
				: base(address, logId)
		{
			Identity = identity;
			VersionSign = versionSign ?? (Tools.IsLinux ? TsVersionSigned.VER_LIN_3_X_X : TsVersionSigned.VER_WIN_3_X_X);
			Username = username ?? "TSLibUser";
			ServerPassword = serverPassword ?? Password.Empty;
			DefaultChannel = defaultChannel ?? string.Empty;
			DefaultChannelPassword = defaultChannelPassword ?? Password.Empty;
		}
	}

	public readonly struct Password
	{
		public static readonly Password Empty = FromHash(string.Empty);

		public string HashedPassword { get; }

		private Password(string hashed) { HashedPassword = hashed; }
		public static Password FromHash(string hash) => new Password(hash);
		public static Password FromPlain(string pass) => new Password(TsCrypt.HashPassword(pass));

		public static implicit operator Password(string pass) => FromPlain(pass);
	}
}
