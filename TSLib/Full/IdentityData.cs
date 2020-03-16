// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using System;

namespace TSLib.Full
{
	/// <summary>Represents the identity of a user.
	/// To generate new identities use <see cref="TsCrypt.GenerateNewIdentity"/>.
	/// To improve the security level of this identity use <see cref="TsCrypt.ImproveSecurity"/>.</summary>
	public class IdentityData
	{
		private string publicKeyString;
		private string privateKeyString;
		private string publicAndPrivateKeyString;

		/// <summary>The public key encoded in base64.</summary>
		public string PublicKeyString => publicKeyString ?? (publicKeyString = TsCrypt.ExportPublicKey(PublicKey));
		/// <summary>The private key encoded in base64.</summary>
		public string PrivateKeyString => privateKeyString ?? (privateKeyString = TsCrypt.ExportPrivateKey(PrivateKey));
		/// <summary>The public and private key encoded in base64.</summary>
		public string PublicAndPrivateKeyString =>
			publicAndPrivateKeyString ?? (publicAndPrivateKeyString = TsCrypt.ExportPublicAndPrivateKey(PublicKey, PrivateKey));
		/// <summary>The public key represented as its cryptographic data structure.</summary>
		public ECPoint PublicKey { get; }
		/// <summary>The private key represented as its cryptographic data structure.</summary>
		public BigInteger PrivateKey { get; }
		/// <summary>A number which is used to determine the security level of this identity.</summary>
		public ulong ValidKeyOffset { get; set; }
		/// <summary>When bruteforcing numbers linearly from 0, the last bruteforced number
		/// can be stored here to resume from when continuing to search.</summary>
		public ulong LastCheckedKeyOffset { get; set; }

		private Uid? clientUid;
		/// <summary>The client uid, which can be used in teamspeak for various features.</summary>
		public Uid ClientUid
		{
			get
			{
				if (clientUid == null)
					clientUid = (Uid)TsCrypt.GetUidFromPublicKey(PublicKeyString);
				return clientUid.Value;
			}
		}

		public IdentityData(BigInteger privateKey, ECPoint publicKey = null)
		{
			PrivateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
			PublicKey = publicKey ?? TsCrypt.RestorePublicFromPrivateKey(privateKey);
		}

		public static bool IsUidValid(string uid)
		{
			if (uid == "anonymous" || uid == "serveradmin")
				return true;
			var result = TsCrypt.Base64Decode(uid);
			return result.Ok && result.Value.Length == 20;
		}
	}
}
