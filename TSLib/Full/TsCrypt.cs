// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Chaos.NaCl.Ed25519Ref10;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;
using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TSLib.Commands;
using TSLib.Helper;

namespace TSLib.Full;

/// <summary>Provides all cryptographic functions needed for the low- and high level TeamSpeak protocol usage.</summary>
public sealed class TsCrypt
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	private const string DummyKeyAndNonceString = "c:\\windows\\system\\firewall32.cpl";
	private static readonly byte[] DummyKey = Encoding.ASCII.GetBytes(DummyKeyAndNonceString.Substring(0, 16));
	private static readonly byte[] DummyIv = Encoding.ASCII.GetBytes(DummyKeyAndNonceString.Substring(16, 16));
	private static readonly (byte[], byte[]) DummyKeyAndNonceTuple = (DummyKey, DummyIv);
	private static readonly byte[] Ts3InitMac = Encoding.ASCII.GetBytes("TS3INIT1");
	private const uint InitVersion = 1566914096; // 3.5.0 [Stable]
	private readonly EaxBlockCipher eaxCipher = new(new AesEngine());

	internal const int MacLen = 8;
	internal const int PacketTypeKinds = 9;

	internal IdentityData Identity { get; }

	internal bool CryptoInitComplete { get; private set; }
	private byte[]? alphaTmp;
	private byte[]? ivStruct;
	private readonly byte[] fakeSignature = new byte[MacLen];
	private readonly (byte[] key, byte[] nonce, uint generation)?[] cachedKeyNonces = new (byte[], byte[], uint)?[PacketTypeKinds * 2];

	public TsCrypt(IdentityData identity)
	{
		Reset();
		Identity = identity;
	}

	internal void Reset()
	{
		CryptoInitComplete = false;
		ivStruct = null;
		Array.Clear(fakeSignature, 0, fakeSignature.Length);
		Array.Clear(cachedKeyNonces, 0, cachedKeyNonces.Length);
	}

	#region TS3INIT1 / CRYPTO INIT

	/// <summary>Calculates and initializes all required variables for the secure communication.</summary>
	/// <param name="alpha">The alpha key from clientinit encoded in base64.</param>
	/// <param name="beta">The beta key from clientinit encoded in base64.</param>
	/// <param name="omega">The omega key from clientinit encoded in base64.</param>
	internal E<string> CryptoInit(string alpha, string beta, string omega)
	{
		var alphaBytes = Base64Decode(alpha);
		if (alphaBytes is null) return "alphaBytes parameter is invalid";
		var betaBytes = Base64Decode(beta);
		if (betaBytes is null) return "betaBytes parameter is invalid";
		var omegaBytes = Base64Decode(omega);
		if (omegaBytes is null) return "omegaBytes parameter is invalid";
		if (!IdentityData.ImportPublicKey(omegaBytes).GetOk(out var serverPublicKey))
			return "server public key is invalid";

		byte[] sharedKey = GetSharedSecret(serverPublicKey);
		return SetSharedSecret(alphaBytes, betaBytes, sharedKey);
	}

	/// <summary>Calculates a shared secred with ECDH from the client private and server public key.</summary>
	/// <param name="publicKeyPoint">The public key of the server.</param>
	/// <returns>Returns a 32 byte shared secret.</returns>
	private byte[] GetSharedSecret(ECPoint publicKeyPoint)
	{
		ECPoint p = publicKeyPoint.Multiply(Identity.PrivateKey).Normalize();
		byte[] keyArr = p.AffineXCoord.ToBigInteger().ToByteArray();
		if (keyArr.Length == 32)
			return Hash1It(keyArr);
		if (keyArr.Length > 32)
			return Hash1It(keyArr, keyArr.Length - 32, 32);
		// else keyArr.Length < 32
		var keyArrExt = new byte[32];
		Array.Copy(keyArr, 0, keyArrExt, 32 - keyArr.Length, keyArr.Length);
		return Hash1It(keyArrExt);
	}

	/// <summary>Initializes all required variables for the secure communication.</summary>
	/// <param name="alpha">The alpha key from clientinit.</param>
	/// <param name="beta">The beta key from clientinit.</param>
	/// <param name="sharedKey">The omega key from clientinit.</param>
	private E<string> SetSharedSecret(ReadOnlySpan<byte> alpha, ReadOnlySpan<byte> beta, ReadOnlySpan<byte> sharedKey)
	{
		if (beta.Length != 10 && beta.Length != 54)
			return $"Invalid beta size ({beta.Length})";

		// prepares the ivstruct consisting of 2 random byte chains of 10/10 or 10/54 bytes which both sides agreed on
		ivStruct = new byte[10 + beta.Length];

		// applying hashes to get the required values for ts
		XorBinary(sharedKey, alpha, alpha.Length, ivStruct);
		XorBinary(sharedKey[10..], beta, beta.Length, ivStruct.AsSpan(10));

		// creating a dummy signature which will be used on packets which dont use a real encryption signature (like plain voice)
		var buffer2 = Hash1It(ivStruct, 0, ivStruct.Length);
		Array.Copy(buffer2, 0, fakeSignature, 0, 8);

		alphaTmp = null;
		CryptoInitComplete = true;
		return R.Ok;
	}

	internal E<string> CryptoInit2(string license, string omega, string proof, string beta, byte[] privateKey)
	{
		var licenseBytes = Base64Decode(license);
		if (licenseBytes is null) return "license parameter is invalid";
		var omegaBytes = Base64Decode(omega);
		if (omegaBytes is null) return "omega parameter is invalid";
		var proofBytes = Base64Decode(proof);
		if (proofBytes is null) return "proof parameter is invalid";
		var betaBytes = Base64Decode(beta);
		if (betaBytes is null) return "beta parameter is invalid";
		if (!IdentityData.ImportPublicKey(omegaBytes).GetOk(out var serverPublicKey))
			return "server public key is invalid";

		// Verify that our connection isn't tampered with
		if (!VerifySign(serverPublicKey, licenseBytes, proofBytes))
			return "The init proof is not valid. Your connection might be tampered with or the server is an idiot.";

		var sw = Stopwatch.StartNew();
		if (!Licenses.Parse(licenseBytes).Get(out var licenseChain, out var error))
			return error;
		Log.Debug("Parsed license successfully in {0:F3}ms", sw.Elapsed.TotalMilliseconds);

		sw.Restart();
		var key = licenseChain.DeriveKey();
		Log.Debug("Processed license successfully in {0:F3}ms", sw.Elapsed.TotalMilliseconds);

		sw.Restart();
		var keyArr = GetSharedSecret2(key, privateKey);
		Log.Debug("Calculated shared secret in {0:F3}ms", sw.Elapsed.TotalMilliseconds);

		return SetSharedSecret(alphaTmp, betaBytes, keyArr);
	}

	private static byte[] GetSharedSecret2(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> privateKey)
	{
		Span<byte> privateKeyCpy = stackalloc byte[32];
		privateKey.CopyTo(privateKeyCpy);
		privateKeyCpy[31] &= 0x7F;
		GroupOperations.ge_frombytes_negate_vartime(out var pub1, publicKey);
		GroupOperations.ge_scalarmult_vartime(out GroupElementP2 mul, privateKeyCpy, pub1);
		Span<byte> sharedTmp = stackalloc byte[32];
		GroupOperations.ge_tobytes(sharedTmp, mul);
		sharedTmp[31] ^= 0x80;
		var bytes = new byte[64];
		Chaos.NaCl.Sha512.Hash(sharedTmp, bytes);
		return bytes;
	}

	internal R<byte[], string> ProcessInit1<TDir>(byte[]? data)
	{
		const int versionLen = 4;
		const int initTypeLen = 1;

		const string packetInvalid = "Invalid Init1 packet";
		const string packetTooShort = packetInvalid + " (too short)";
		const string packetInvalidStep = packetInvalid + " (invalid step)";
		const string packetInvalidLength = packetInvalid + " (invalid length)";

		int type;
		byte[] sendData;
		if (data != null)
		{
			if (Packet<TDir>.FromServer)
			{
				if (data.Length < initTypeLen)
					return packetTooShort;
				type = data[0];
				if (type != 1 && type != 3 && type != 0x7F)
					return packetInvalidStep;
			}
			else
			{
				if (data.Length < versionLen + initTypeLen)
					return packetTooShort;
				type = data[4];
				if (type != 0 && type != 2 && type != 4)
					return packetInvalidStep;
			}
		}
		else type = -1;

		if (data == null || type == 0x7F)
		{
			// 0x7F: Some strange servers do this
			// the normal client responds by starting again
			sendData = new byte[versionLen + initTypeLen + 4 + 4 + 8];
			BinaryPrimitives.WriteUInt32BigEndian(sendData.AsSpan(0), InitVersion); // initVersion
			sendData[versionLen] = 0x00; // initType
			BinaryPrimitives.WriteUInt32BigEndian(sendData.AsSpan(versionLen + initTypeLen), Tools.UnixNow); // 4byte timestamp
			BinaryPrimitives.WriteInt32BigEndian(sendData.AsSpan(versionLen + initTypeLen + 4), Tools.Random.Next()); // 4byte random
			return sendData;
		}

		switch (type)
		{
		case 0:
			if (data.Length != 21)
				return packetInvalidLength;
			sendData = new byte[initTypeLen + 16 + 4];
			sendData[0] = 0x01; // initType
			BinaryPrimitives.WriteUInt32BigEndian(sendData.AsSpan(initTypeLen + 16), BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(versionLen + initTypeLen + 4)));
			return sendData;

		case 1:
			switch (data.Length)
			{
			case 21:
				sendData = new byte[versionLen + initTypeLen + 16 + 4];
				BinaryPrimitives.WriteUInt32BigEndian(sendData.AsSpan(0), InitVersion); // initVersion
				sendData[versionLen] = 0x02; // initType
				Array.Copy(data, initTypeLen, sendData, versionLen + initTypeLen, 20);
				return sendData;
			case 5:
				var errorNum = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(initTypeLen));
				if (Enum.IsDefined(typeof(TsErrorCode), errorNum))
					return $"Got Init1(1) error: {(TsErrorCode)errorNum}";
				return $"Got Init1(1) undefined error code: {errorNum}";
			default:
				return packetInvalidLength;
			}

		case 2:
			if (data.Length != versionLen + initTypeLen + 16 + 4)
				return packetInvalidLength;
			sendData = new byte[initTypeLen + 64 + 64 + 4 + 100];
			sendData[0] = 0x03; // initType
			sendData[initTypeLen + 64 - 1] = 1; // dummy x to 1
			sendData[initTypeLen + 64 + 64 - 1] = 1; // dummy n to 1
			BinaryPrimitives.WriteInt32BigEndian(sendData.AsSpan(initTypeLen + 64 + 64), 1); // dummy level to 1
			return sendData;

		case 3:
			if (data.Length != initTypeLen + 64 + 64 + 4 + 100)
				return packetInvalidLength;
			alphaTmp = new byte[10];
			Tools.Random.NextBytes(alphaTmp);
			var alpha = Convert.ToBase64String(alphaTmp);
			string initAdd = TsCommand.BuildToString("clientinitiv",
				new ICommandPart[] {
						new CommandParameter("alpha", alpha),
						new CommandParameter("omega", Identity.PublicKeyString),
						new CommandParameter("ot", 1),
						new CommandParameter("ip", string.Empty) });
			var textBytes = Tools.Utf8Encoder.GetBytes(initAdd);

			// Prepare solution
			int level = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(initTypeLen + 128));
			if (!SolveRsaChallange(data, initTypeLen, level).Get(out var y, out var error))
				return error;

			// Copy bytes for this result: [Version..., InitType..., data..., y..., text...]
			sendData = new byte[versionLen + initTypeLen + 64 + 64 + 4 + 100 + 64 + textBytes.Length];
			// Copy initVersion
			BinaryPrimitives.WriteUInt32BigEndian(sendData.AsSpan(0), InitVersion);
			// Write InitType
			sendData[versionLen] = 0x04;
			// Copy data
			Array.Copy(data, initTypeLen, sendData, versionLen + initTypeLen, 232);
			// Copy y
			Array.Copy(y, 0, sendData, versionLen + initTypeLen + 232 + (64 - y.Length), y.Length);
			// Copy text
			Array.Copy(textBytes, 0, sendData, versionLen + initTypeLen + 232 + 64, textBytes.Length);
			return sendData;

		case 4:
			if (data.Length < versionLen + initTypeLen + 64 + 64 + 4 + 100 + 64)
				return packetTooShort;
			// TODO check result
			return Array.Empty<byte>();

		default:
			return $"Got invalid Init1({type}) packet id";
		}
	}

	/// <summary>This method calculates x ^ (2^level) % n = y which is the solution to the server RSA puzzle.</summary>
	/// <param name="data">The data array, containing x=[0,63] and n=[64,127], each unsigned, as a BigInteger bytearray.</param>
	/// <param name="offset">The offset of x and n in the data array.</param>
	/// <param name="level">The exponent to x.</param>
	/// <returns>The y value, unsigned, as a BigInteger bytearray.</returns>
	private static R<byte[], string> SolveRsaChallange(byte[] data, int offset, int level)
	{
		if (level < 0 || level > 1_000_000)
			return "RSA challange level is not within an acceptable range";

		// x is the base, n is the modulus.
		var x = new BigInteger(1, data, 00 + offset, 64);
		var n = new BigInteger(1, data, 64 + offset, 64);
		return x.ModPow(BigInteger.Two.Pow(level), n).ToByteArrayUnsigned();
	}

	internal static (byte[] publicKey, byte[] privateKey) GenerateTemporaryKey()
	{
		var privateKey = new byte[32];
		using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
			rng.GetBytes(privateKey);
		ScalarOperations.sc_clamp(privateKey);

		GroupOperations.ge_scalarmult_base(out var A, privateKey);
		var publicKey = new byte[32];
		GroupOperations.ge_p3_tobytes(publicKey, A);

		return (publicKey, privateKey);
	}

	#endregion

	#region ENCRYPTION/DECRYPTION

	internal void Encrypt<TDir>(ref Packet<TDir> packet)
	{
		if (packet.PacketType == PacketType.Init1)
		{
			FakeEncrypt(ref packet, Ts3InitMac);
			return;
		}
		if (packet.UnencryptedFlag)
		{
			FakeEncrypt(ref packet, fakeSignature);
			return;
		}

		var (key, nonce) = GetKeyNonce(Packet<TDir>.FromServer, packet.PacketId, packet.GenerationId, packet.PacketType, !CryptoInitComplete);
		packet.BuildHeader();
		ICipherParameters ivAndKey = new AeadParameters(new KeyParameter(key), 8 * MacLen, nonce, packet.Header);

		byte[] result;
		int len;
		lock (eaxCipher)
		{
			eaxCipher.Init(true, ivAndKey);
			result = new byte[eaxCipher.GetOutputSize(packet.Size)];
			try
			{
				len = eaxCipher.ProcessBytes(packet.Data, 0, packet.Size, result, 0);
				len += eaxCipher.DoFinal(result, len);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Internal encryption error.");
				throw;
			}
		}

		// result consists of [Data..., Mac...]
		// to build the final TS/libtomcrypt we need to copy it into another order

		// len is Data.Length + Mac.Length
		// //packet.Raw = new byte[Packet<TDir>.HeaderLength + len];
		// Copy the Mac from [Data..., Mac...] to [Mac..., Header..., Data...]
		Array.Copy(result, len - MacLen, packet.Raw, 0, MacLen);
		// Copy the Header from packet.Header to [Mac..., Header..., Data...]
		Array.Copy(packet.Header, 0, packet.Raw, MacLen, Packet<TDir>.HeaderLength);
		// Copy the Data from [Data..., Mac...] to [Mac..., Header..., Data...]
		Array.Copy(result, 0, packet.Raw, MacLen + Packet<TDir>.HeaderLength, len - MacLen);
		// Raw is now [Mac..., Header..., Data...]
	}

	private static void FakeEncrypt<TDir>(ref Packet<TDir> packet, byte[] mac)
	{
		// //packet.Raw = new byte[packet.Data.Length + MacLen + Packet<TDir>.HeaderLength];
		// Copy the Mac from [Mac...] to [Mac..., Header..., Data...]
		Array.Copy(mac, 0, packet.Raw, 0, MacLen);
		// Copy the Header from packet.Header to [Mac..., Header..., Data...]
		packet.BuildHeader(packet.Raw.AsSpan(MacLen, Packet<TDir>.HeaderLength));
		// Copy the Data from packet.Data to [Mac..., Header..., Data...]
		Array.Copy(packet.Data, 0, packet.Raw, MacLen + Packet<TDir>.HeaderLength, packet.Data.Length);
		// Raw is now [Mac..., Header..., Data...]
	}

	internal bool Decrypt<TDir>(ref Packet<TDir> packet)
	{
		if (packet.PacketType == PacketType.Init1)
			return FakeDecrypt(ref packet, Ts3InitMac);

		if (packet.UnencryptedFlag)
			return FakeDecrypt(ref packet, fakeSignature);

		var decryptResult = DecryptData(ref packet, !CryptoInitComplete);
		if (decryptResult)
			return true;

		// This is a hacky workaround for a special ack:
		// We send these two packets simultaneously:
		// - [Id:1] clientek    (dummy-encrypted)
		// - [Id:2] clientinit  (session-encrypted)
		// We get an ack for each with the same encryption scheme.
		// We can't know for sure which ack comes first and therefore
		//  whether the dummy or session key should be used.
		// In case we actually picked the wrong key, try it again
		//  with the dummy key.
		if (packet.PacketType == PacketType.Ack && packet.PacketId <= 2)
		{
			Log.Debug("Using shady ack workaround.");
			return DecryptData(ref packet, true);
		}
		else
		{
			return false;
		}
	}

	private bool DecryptData<TDir>(ref Packet<TDir> packet, bool dummyEncryption)
	{
		Array.Copy(packet.Raw, MacLen, packet.Header, 0, Packet<TDir>.HeaderLength);
		var (key, nonce) = GetKeyNonce(Packet<TDir>.FromServer, packet.PacketId, packet.GenerationId, packet.PacketType, dummyEncryption);
		int dataLen = packet.Raw.Length - (MacLen + Packet<TDir>.HeaderLength);

		ICipherParameters ivAndKey = new AeadParameters(new KeyParameter(key), 8 * MacLen, nonce, packet.Header);
		try
		{
			byte[] result;
			lock (eaxCipher)
			{
				eaxCipher.Init(false, ivAndKey);
				result = new byte[eaxCipher.GetOutputSize(dataLen + MacLen)];

				int len = eaxCipher.ProcessBytes(packet.Raw, MacLen + Packet<TDir>.HeaderLength, dataLen, result, 0);
				len += eaxCipher.ProcessBytes(packet.Raw, 0, MacLen, result, len);
				len += eaxCipher.DoFinal(result, len);

				if (len != dataLen)
					return false;
			}

			packet.Data = result;
		}
		catch (Exception) { return false; }
		return true;
	}

	private static bool FakeDecrypt<TDir>(ref Packet<TDir> packet, byte[] mac)
	{
		if (!CheckEqual(packet.Raw, mac, MacLen))
			return false;
		int dataLen = packet.Raw.Length - (MacLen + Packet<TDir>.HeaderLength);
		packet.Data = new byte[dataLen];
		Array.Copy(packet.Raw, MacLen + Packet<TDir>.HeaderLength, packet.Data, 0, dataLen);
		return true;
	}

	/// <summary>TS uses a new key and nonce for each packet sent and received. This method generates and caches these.</summary>
	/// <param name="fromServer">True if the packet is from server to client, false for client to server.</param>
	/// <param name="packetId">The id of the packet, host order.</param>
	/// <param name="generationId">Each time the packetId reaches 65535 the next packet will go on with 0 and the generationId will be increased by 1.</param>
	/// <param name="packetType">The packetType.</param>
	/// <param name="dummyEncryption">Returns the const dummy (key,nonce) when true, ignoring all other parameters.</param>
	/// <returns>A tuple of (key, nonce)</returns>
	private (byte[] key, byte[] nonce) GetKeyNonce(bool fromServer, ushort packetId, uint generationId, PacketType packetType, bool dummyEncryption)
	{
		if (dummyEncryption)
			return DummyKeyAndNonceTuple;

		// only the lower 4 bits are used for the real packetType
		var packetTypeRaw = (byte)packetType;

		int cacheIndex = packetTypeRaw + (fromServer ? 0 : PacketTypeKinds);
		ref var cacheValue = ref cachedKeyNonces[cacheIndex];
		if (!cacheValue.HasValue || cacheValue.GetValueOrDefault().generation != generationId)
		{
			// this part of the key/nonce is fixed by the message direction and packetType

			var tmpToHash = new byte[ivStruct!.Length == 20 ? 26 : 70];

			tmpToHash[0] = fromServer ? (byte)0x30 : (byte)0x31;
			tmpToHash[1] = packetTypeRaw;

			BinaryPrimitives.WriteUInt32BigEndian(tmpToHash.AsSpan(2), generationId);
			Array.Copy(ivStruct, 0, tmpToHash, 6, ivStruct.Length);

			var result = Hash256It(tmpToHash).AsSpan();

			cacheValue = (result[0..16].ToArray(), result[16..32].ToArray(), generationId);
		}

		var key = new byte[16];
		var nonce = new byte[16];
		Array.Copy(cacheValue.GetValueOrDefault().key, 0, key, 0, 16);
		Array.Copy(cacheValue.GetValueOrDefault().nonce, 0, nonce, 0, 16);

		// finally the first two bytes get xor'd with the packet id
		key[0] ^= unchecked((byte)(packetId >> 8));
		key[1] ^= unchecked((byte)(packetId >> 0));

		return (key, nonce);
	}

	#endregion

	#region CRYPT HELPER

	private static bool CheckEqual(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2, int len)
	{
		if (a1.Length < len) throw new ArgumentOutOfRangeException(nameof(a1), "is shorter than the requested compare length");
		if (a2.Length < len) throw new ArgumentOutOfRangeException(nameof(a2), "is shorter than the requested compare length");

		int res = 0;
		for (int i = 0; i < len; i++)
			res |= a1[i] ^ a2[i];
		return res == 0;
	}

	internal static void XorBinary(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, int len, Span<byte> outBuf)
	{
		if (a.Length < len) throw new ArgumentOutOfRangeException(nameof(a), "is shorter than the requested xor length");
		if (b.Length < len) throw new ArgumentOutOfRangeException(nameof(b), "is shorter than the requested xor length");
		if (outBuf.Length < len) throw new ArgumentOutOfRangeException(nameof(outBuf), "is shorter than the requested xor length");
		for (int i = 0; i < len; i++)
			outBuf[i] = (byte)(a[i] ^ b[i]);
	}

	private static readonly System.Security.Cryptography.SHA1 Sha1Hash = System.Security.Cryptography.SHA1.Create();
	private static readonly System.Security.Cryptography.SHA256 Sha256Hash = System.Security.Cryptography.SHA256.Create();
	private static readonly System.Security.Cryptography.SHA512 Sha512Hash = System.Security.Cryptography.SHA512.Create();
	internal static byte[] Hash1It(byte[] data, int offset = 0, int len = 0) => HashItInternal(Sha1Hash, data, offset, len);
	internal static byte[] Hash256It(byte[] data, int offset = 0, int len = 0) => HashItInternal(Sha256Hash, data, offset, len);
	internal static byte[] Hash512It(byte[] data, int offset = 0, int len = 0) => HashItInternal(Sha512Hash, data, offset, len);
	private static byte[] HashItInternal(System.Security.Cryptography.HashAlgorithm hashAlgo, byte[] data, int offset = 0, int len = 0)
	{
		lock (hashAlgo)
		{
			return hashAlgo.ComputeHash(data, offset, len == 0 ? data.Length - offset : len);
		}
	}

#if !NETSTANDARD2_0
	internal static void Hash1It(ReadOnlySpan<byte> data, Span<byte> hash) => HashItInternal(Sha1Hash, data, hash);
	internal static void Hash256It(ReadOnlySpan<byte> data, Span<byte> hash) => HashItInternal(Sha256Hash, data, hash);
	internal static void Hash512It(ReadOnlySpan<byte> data, Span<byte> hash) => HashItInternal(Sha512Hash, data, hash);
	private static void HashItInternal(System.Security.Cryptography.HashAlgorithm hashAlgo, ReadOnlySpan<byte> data, Span<byte> hash)
	{
		lock (hashAlgo)
		{
			if (!hashAlgo.TryComputeHash(data, hash, out var len))
				throw new InvalidOperationException();
			hash = hash[..len];
		}
	}
#endif

	/// <summary>
	/// Hashes a password like TeamSpeak.
	/// The hash works like this: base64(sha1(password))
	/// </summary>
	/// <param name="password">The password to hash.</param>
	/// <returns>The hashed password.</returns>
	public static string HashPassword(string password)
	{
		if (string.IsNullOrEmpty(password))
			return string.Empty;
		var bytes = Tools.Utf8Encoder.GetBytes(password);
		var hashed = Hash1It(bytes);
		return Convert.ToBase64String(hashed);
	}

	public static byte[] Sign(BigInteger privateKey, byte[] data)
	{
		var signer = SignerUtilities.GetSigner(X9ObjectIdentifiers.ECDsaWithSha256);
		var signKey = new ECPrivateKeyParameters(privateKey, IdentityData.KeyGenParams.DomainParameters);
		signer.Init(true, signKey);
		signer.BlockUpdate(data, 0, data.Length);
		return signer.GenerateSignature();
	}

	public static bool VerifySign(ECPoint publicKey, byte[] data, byte[] proof)
	{
		var signer = SignerUtilities.GetSigner(X9ObjectIdentifiers.ECDsaWithSha256);
		var signKey = new ECPublicKeyParameters(publicKey, IdentityData.KeyGenParams.DomainParameters);
		signer.Init(false, signKey);
		signer.BlockUpdate(data, 0, data.Length);
		return signer.VerifySignature(proof);
	}

	private static readonly byte[] TsVersionSignPublicKey = Convert.FromBase64String("UrN1jX0dBE1vulTNLCoYwrVpfITyo+NBuq/twbf9hLw=");

	public static bool EdCheck(TsVersionSigned sign)
	{
		var ver = Encoding.ASCII.GetBytes(sign.Platform + sign.Version);
		var signArr = Base64Decode(sign.Sign);
		if (signArr is null)
			return false;
		return Chaos.NaCl.Ed25519.Verify(signArr, ver, TsVersionSignPublicKey);
	}

	public static void VersionSelfCheck()
	{
		var versions = typeof(TsVersionSigned).GetProperties().Where(prop => prop.PropertyType == typeof(TsVersionSigned));
		foreach (var ver in versions)
		{
			var verObj = (TsVersionSigned)ver.GetValue(null)!;
			if (!EdCheck(verObj))
				throw new Exception($"Version is invalid: {verObj}");
		}
	}

	internal static byte[]? Base64Decode(string str)
	{
		try { return Convert.FromBase64String(str); }
		catch (FormatException) { return null; }
	}

	#endregion
}
