// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;
using System;
using System.Buffers.Text;
using System.Text;
using System.Text.RegularExpressions;

namespace TSLib.Full
{
	/// <summary>Represents the identity of a user.
	/// To generate new identities use <see cref="TsCrypt.GenerateNewIdentity"/>.
	/// To improve the security level of this identity use <see cref="TsCrypt.ImproveSecurity"/>.</summary>
	public class IdentityData
	{
		private string? publicKeyString;
		private string? privateKeyString;
		private string? publicAndPrivateKeyString;

		/// <summary>The public key encoded in base64.</summary>
		public string PublicKeyString => publicKeyString ??= Convert.ToBase64String(ExportPublicKey(PublicKey));
		/// <summary>The private key encoded in base64.</summary>
		public string PrivateKeyString => privateKeyString ??= Convert.ToBase64String(ExportPrivateKey(PrivateKey));
		/// <summary>The public and private key encoded in base64.</summary>
		public string PublicAndPrivateKeyString => publicAndPrivateKeyString ??= Convert.ToBase64String(ExportPrivateAndPublicKey(PrivateKey, PublicKey));
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
		public Uid ClientUid => clientUid ?? (clientUid = (Uid)GetUidFromPublicKey(PublicKeyString)).Value;

		public IdentityData(BigInteger privateKey, ECPoint? publicKey = null, ulong keyOffset = 0, ulong lastCheckedKeyOffset = 0)
		{
			PrivateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
			PublicKey = publicKey ?? RestorePublicFromPrivateKey(privateKey);
			ValidKeyOffset = keyOffset;
			LastCheckedKeyOffset = lastCheckedKeyOffset < keyOffset ? keyOffset : lastCheckedKeyOffset;
		}

		internal static readonly ECKeyGenerationParameters KeyGenParams = new ECKeyGenerationParameters(X9ObjectIdentifiers.Prime256v1, new SecureRandom());
		private static readonly Regex IdentityRegex = new Regex(@"^(?<level>\d+)V(?<identity>[\w\/\+]+={0,2})$", RegexOptions.ECMAScript | RegexOptions.CultureInvariant);
		private static readonly byte[] TsIdentityObfuscationKey = Encoding.ASCII.GetBytes("b9dfaa7bee6ac57ac7b65f1094a1c155e747327bc2fe5d51c512023fe54a280201004e90ad1daaae1075d53b7d571c30e063b5a62a4a017bb394833aa0983e6e");

		#region KEY IMPORT/EXPROT

		/// <summary>
		/// Detects the kind of key and creates an identity from it.
		/// This method can import 3 kinds of identity keys.
		/// <list type="bullet">
		/// <item><description>The Teamspeak 3 key as it is stored by the normal client.</description></item>
		/// <item><description>A libtomcrypt public+private key export. (+KeyOffset).</description></item>
		/// <item><description>A TSLib private-only key export. (+KeyOffset).</description></item>
		/// </list>
		/// Keys with "(+KeyOffset)" should add the key offset for the security level in the separate parameter.
		/// </summary>
		/// <param name="any">The identity string.</param>
		/// <param name="keyOffset">A number which determines the security level of an identity.</param>
		/// <param name="lastCheckedKeyOffset">The last brute forced number. Default 0: will take the current keyOffset.</param>
		/// <returns>The identity information.</returns>
		public static R<IdentityData, string> FromAny(string any, ulong keyOffset = 0, ulong lastCheckedKeyOffset = 0)
		{
			if (FromTsIdentity(any).GetOk(out var tsIdentity))
				return tsIdentity;
			return FromBase64(any, keyOffset, lastCheckedKeyOffset);
		}

		/// <summary>This methods loads a secret identity.</summary>
		/// <param name="key">The key stored in base64, encoded like the libtomcrypt export method of a private key.
		/// Or the TSLib's shorted private-only key.</param>
		/// <param name="keyOffset">A number which determines the security level of an identity.</param>
		/// <param name="lastCheckedKeyOffset">The last brute forced number. Default 0: will take the current keyOffset.</param>
		/// <returns>The identity information.</returns>
		public static R<IdentityData, string> FromBase64(string key, ulong keyOffset, ulong lastCheckedKeyOffset = 0)
		{
			var asnByteArray = TsCrypt.Base64Decode(key);
			if (asnByteArray is null)
				return "Invalid identity base64 string";
			var importRes = ImportKeyDynamic(asnByteArray);
			if (!importRes.Ok)
				return importRes.Error;
			var (privateKey, publicKey) = importRes.Value;
			if (privateKey is null)
				return "Key string did not contain a private key";
			return new IdentityData(privateKey, publicKey, keyOffset, lastCheckedKeyOffset);
		}

		public static R<IdentityData, string> FromTsIdentity(string identity)
		{
			var match = IdentityRegex.Match(identity);
			if (!match.Success)
				return "Identity could not get matched as teamspeak identity";

			if (!ulong.TryParse(match.Groups["level"].Value, out var level))
				return "Invalid key offset";

			var ident = TsCrypt.Base64Decode(match.Groups["identity"].Value);
			if (ident is null)
				return "Invalid identity base64 string";

			if (ident.Length < 20)
				return "Identity too short";

			int nullIdx = ident.AsSpan(20).IndexOf((byte)0);
			var hash = TsCrypt.Hash1It(ident, 20, nullIdx < 0 ? ident.Length - 20 : nullIdx);

			TsCrypt.XorBinary(ident, hash, 20, ident);
			TsCrypt.XorBinary(ident, TsIdentityObfuscationKey, Math.Min(100, ident.Length), ident);

			if (Base64.DecodeFromUtf8InPlace(ident, out var length) != System.Buffers.OperationStatus.Done)
				return "Invalid deobfuscated base64 string";

			if (!ImportKeyDynamic(ident.AsSpan(0, length).ToArray()).Get(out var importRes, out var error))
				return error;

			var (privateKey, publicKey) = importRes;
			if (privateKey is null)
				return "Key string did not contain a private key";
			return new IdentityData(privateKey, publicKey, level);
		}

		public string ToTsIdentity()
		{
			var ident = ExportPrivateAndPublicKey(PrivateKey, PublicKey);
			var encMaxLen = Base64.GetMaxEncodedToUtf8Length(ident.Length);
			var final = new byte[encMaxLen];
			if (Base64.EncodeToUtf8(ident, final, out _, out var length) != System.Buffers.OperationStatus.Done)
				throw new InvalidOperationException();
			TsCrypt.XorBinary(final, TsIdentityObfuscationKey, Math.Min(100, length), final);
			int nullIdx = final.AsSpan(20).IndexOf((byte)0);
			var hash = TsCrypt.Hash1It(final, 20, nullIdx < 0 ? length - 20 : nullIdx);
			TsCrypt.XorBinary(final, hash, 20, final);
			return ValidKeyOffset + "V" + Convert.ToBase64String(final);
		}

		internal static R<ECPoint, string> ImportPublicKey(byte[] asnByteArray)
		{
			try
			{
				var asnKeyData = (DerSequence)Asn1Object.FromByteArray(asnByteArray);
				var x = ((DerInteger)asnKeyData[2]).Value;
				var y = ((DerInteger)asnKeyData[3]).Value;

				var ecPoint = KeyGenParams.DomainParameters.Curve.CreatePoint(x, y);
				return ecPoint;
			}
			catch (Exception) { return "Could not import public key"; }
		}

		private static R<(BigInteger? privateKey, ECPoint? publicKey), string> ImportKeyDynamic(byte[] asnByteArray)
		{
			BigInteger? privateKey = null;
			ECPoint? publicKey = null;
			try
			{
				var asnKeyData = (DerSequence)Asn1Object.FromByteArray(asnByteArray);
				var bitInfo = ((DerBitString)asnKeyData[0]).IntValue;
				if (bitInfo == 0b0000_0000 || bitInfo == 0b1000_0000)
				{
					var x = ((DerInteger)asnKeyData[2]).Value;
					var y = ((DerInteger)asnKeyData[3]).Value;
					publicKey = KeyGenParams.DomainParameters.Curve.CreatePoint(x, y);

					if (bitInfo == 0b1000_0000)
					{
						privateKey = ((DerInteger)asnKeyData[4]).Value;
					}
				}
				else if (bitInfo == 0b1100_0000)
				{
					privateKey = ((DerInteger)asnKeyData[2]).Value;
				}
			}
			catch (Exception ex) { return $"Could not import identity: {ex.Message}"; }
			return (privateKey, publicKey);
		}

		private static byte[] ExportPublicKey(ECPoint publicKey)
			=> new DerSequence(
				new DerBitString(new byte[] { 0b0000_0000 }, 7),
				new DerInteger(32),
				new DerInteger(publicKey.AffineXCoord.ToBigInteger()),
				new DerInteger(publicKey.AffineYCoord.ToBigInteger())).GetDerEncoded();

		private static byte[] ExportPrivateKey(BigInteger privateKey)
			=> new DerSequence(
				new DerBitString(new byte[] { 0b1100_0000 }, 6),
				new DerInteger(32),
				new DerInteger(privateKey)).GetDerEncoded();

		private static byte[] ExportPrivateAndPublicKey(BigInteger privateKey, ECPoint publicKey)
			=> new DerSequence(
				new DerBitString(new byte[] { 0b1000_0000 }, 7),
				new DerInteger(32),
				new DerInteger(publicKey.AffineXCoord.ToBigInteger()),
				new DerInteger(publicKey.AffineYCoord.ToBigInteger()),
				new DerInteger(privateKey)).GetDerEncoded();

		private static string GetUidFromPublicKey(string publicKey)
		{
			var publicKeyBytes = Encoding.ASCII.GetBytes(publicKey);
			var hashBytes = TsCrypt.Hash1It(publicKeyBytes);
			return Convert.ToBase64String(hashBytes);
		}

		private static ECPoint RestorePublicFromPrivateKey(BigInteger privateKey)
		{
			var curve = ECNamedCurveTable.GetByOid(X9ObjectIdentifiers.Prime256v1);
			return curve.G.Multiply(privateKey).Normalize();
		}

		#endregion

		#region IDENTITY & SECURITY LEVEL

		/// <summary>Equals ulong.MaxValue.ToString().Length</summary>
		private const int MaxUlongStringLen = 20;

		/// <summary><para>Tries to improve the security level of the provided identity to the new level.</para>
		/// <para>The algorithm takes approximately 2^toLevel milliseconds to calculate; so be careful!</para>
		/// This method can be canceled anytime since progress which is not enough for the next level
		/// will be saved in <see cref="IdentityData.LastCheckedKeyOffset"/> continuously.</summary>
		/// <param name="identity">The identity to improve.</param>
		/// <param name="toLevel">The targeted level.</param>
		public void ImproveSecurity(int toLevel)
		{
			var hashBuffer = new byte[PublicKeyString.Length + MaxUlongStringLen];
			var pubKeyBytes = Encoding.ASCII.GetBytes(PublicKeyString);
			Array.Copy(pubKeyBytes, 0, hashBuffer, 0, pubKeyBytes.Length);

			LastCheckedKeyOffset = Math.Max(ValidKeyOffset, LastCheckedKeyOffset);
			int best = GetSecurityLevel(hashBuffer, pubKeyBytes.Length, ValidKeyOffset);
			while (true)
			{
				if (best >= toLevel) return;

				int curr = GetSecurityLevel(hashBuffer, pubKeyBytes.Length, LastCheckedKeyOffset);
				if (curr > best)
				{
					ValidKeyOffset = LastCheckedKeyOffset;
					best = curr;
				}
				LastCheckedKeyOffset++;
			}
		}

		public int GetSecurityLevel()
		{
			var hashBuffer = new byte[PublicKeyString.Length + MaxUlongStringLen];
			var pubKeyBytes = Encoding.ASCII.GetBytes(PublicKeyString);
			Array.Copy(pubKeyBytes, 0, hashBuffer, 0, pubKeyBytes.Length);
			return GetSecurityLevel(hashBuffer, pubKeyBytes.Length, ValidKeyOffset);
		}

		/// <summary>Creates a new TeamSpeak3 identity.</summary>
		/// <param name="securityLevel">Minimum security level this identity will have.</param>
		/// <returns>The identity information.</returns>
		public static IdentityData GenerateNewIdentity(int securityLevel = 8)
		{
			var ecp = ECNamedCurveTable.GetByName("prime256v1");
			var domainParams = new ECDomainParameters(ecp.Curve, ecp.G, ecp.N, ecp.H, ecp.GetSeed());
			var keyGenParams = new ECKeyGenerationParameters(domainParams, new SecureRandom());
			var generator = new ECKeyPairGenerator();
			generator.Init(keyGenParams);
			var keyPair = generator.GenerateKeyPair();

			var privateKey = (ECPrivateKeyParameters)keyPair.Private;
			var publicKey = (ECPublicKeyParameters)keyPair.Public;

			var identity = new IdentityData(privateKey.D, publicKey.Q.Normalize());
			if (securityLevel > 0)
				identity.ImproveSecurity(securityLevel);
			return identity;
		}

		private static int GetSecurityLevel(byte[] hashBuffer, int pubKeyLen, ulong offset)
		{
			var numBuffer = new byte[MaxUlongStringLen];
			int numLen = 0;
			do
			{
				numBuffer[numLen] = (byte)('0' + (offset % 10));
				offset /= 10;
				numLen++;
			} while (offset > 0);
			for (int i = 0; i < numLen; i++)
				hashBuffer[pubKeyLen + i] = numBuffer[numLen - (i + 1)];
			byte[] outHash = TsCrypt.Hash1It(hashBuffer, 0, pubKeyLen + numLen);

			return GetLeadingZeroBits(outHash);
		}

		private static int GetLeadingZeroBits(byte[] data)
		{
			// TODO dnc 3.0 sse ?
			int curr = 0;
			int i;
			for (i = 0; i < data.Length; i++)
				if (data[i] == 0) curr += 8;
				else break;
			if (i < data.Length)
				for (int bit = 0; bit < 8; bit++)
					if ((data[i] & (1 << bit)) == 0) curr++;
					else break;
			return curr;
		}

		#endregion
	}
}
