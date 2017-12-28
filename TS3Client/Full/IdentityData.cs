// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Full
{
	using Org.BouncyCastle.Math;
	using Org.BouncyCastle.Math.EC;
	using System;

	/// <summary>Represents the identity of a user.
	/// To generate new identities use <see cref="Ts3Crypt.GenerateNewIdentity"/>.
	/// To improve the security level of this identity use <see cref="Ts3Crypt.ImproveSecurity"/>.</summary>
	public class IdentityData
	{
		private string publicKeyString;
		private string privateKeyString;
		private string publicAndPrivateKeyString;

		/// <summary>The public key encoded in base64.</summary>
		public string PublicKeyString => publicKeyString ?? (publicKeyString = Ts3Crypt.ExportPublicKey(PublicKey));
		/// <summary>The private key encoded in base64.</summary>
		public string PrivateKeyString => privateKeyString ?? (privateKeyString = Ts3Crypt.ExportPrivateKey(PrivateKey));
		/// <summary>The public and private key encoded in base64.</summary>
		public string PublicAndPrivateKeyString =>
			publicAndPrivateKeyString ?? (publicAndPrivateKeyString = Ts3Crypt.ExportPublicAndPrivateKey(PublicKey, PrivateKey));
		/// <summary>The public key represented as its cryptographic data structure.</summary>
		public ECPoint PublicKey { get; }
		/// <summary>The private key represented as its cryptographic data structure.</summary>
		public BigInteger PrivateKey { get; }
		/// <summary>A number which is used to determine the security level of this identity.</summary>
		public ulong ValidKeyOffset { get; set; }
		/// <summary>When bruteforcing numbers linearly from 0, the last bruteforced number
		/// can be stored here to resume from when continuing to search.</summary>
		public ulong LastCheckedKeyOffset { get; set; }

		private string clientUid;
		/// <summary>The client uid, which can be used in teamspeak for various features.</summary>
		public string ClientUid => clientUid ?? (clientUid = Ts3Crypt.GetUidFromPublicKey(PublicKeyString));

		public IdentityData(BigInteger privateKey, ECPoint publicKey = null)
		{
			PrivateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
			PublicKey = publicKey ?? Ts3Crypt.RestorePublicFromPrivateKey(privateKey);
		}
	}
}
