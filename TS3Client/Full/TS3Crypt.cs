using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;

namespace TS3Client.Full
{
	using Org.BouncyCastle.Asn1;
	using Org.BouncyCastle.Asn1.X9;
	using Org.BouncyCastle.Crypto.Digests;
	using Org.BouncyCastle.Crypto.Parameters;
	using Org.BouncyCastle.Math;
	using Org.BouncyCastle.Math.EC;
	using Org.BouncyCastle.Security;
	using System;
	using System.Linq;
	using System.Text;

	class TS3Crypt
	{
		private const string DummyKeyAndNonceString = "c:\\windows\\system\\firewall32.cpl";
		private static readonly byte[] DummyKey = Encoding.ASCII.GetBytes(DummyKeyAndNonceString.Substring(0, 16));
		private static readonly byte[] DummyIv = Encoding.ASCII.GetBytes(DummyKeyAndNonceString.Substring(16, 16));
		private static readonly Tuple<byte[], byte[]> DummyKeyAndNonceTuple = new Tuple<byte[], byte[]>(DummyKey, DummyIv);
		private static readonly byte[] TS3InitMac = Encoding.ASCII.GetBytes("TS3INIT1");
		private static readonly byte[] Initversion = new byte[] { 0x06, 0x3b, 0xec, 0xe9 };
		private readonly EaxBlockCipher eaxCipher = new EaxBlockCipher(new AesEngine());

		private const int MacLen = 8;
		private const int OutHeaderLen = 5;
		private const int InHeaderLen = 3;
		private const int PacketTypeKinds = 9;

		private ECPoint publicKey;
		private BigInteger privateKey;

		public bool cryptoInitComplete { get; private set; }
		private byte[] ivStruct = new byte[20];
		private byte[] fakeSignature = new byte[MacLen];
		private readonly Tuple<byte[], byte[]>[] cachedKeyNonces = new Tuple<byte[], byte[]>[PacketTypeKinds * 2];

		public TS3Crypt()
		{
			Reset();
		}

		public void Reset()
		{
			cryptoInitComplete = false;
			fakeSignature = null;
			Array.Clear(ivStruct, 0, ivStruct.Length);
			Array.Clear(cachedKeyNonces, 0, cachedKeyNonces.Length);
		}

		#region KEY IMPORT/EXPROT

		/// <summary>This methods loads the public and private key of our own identity.</summary>
		/// <param name="key">The key stored in base64, encoded like the libtomcrypt export method (of a private key).</param>
		public void ImportOwnKeys(string key)
		{
			// Note: libtomcrypt stores the private AND public key when exporting a private key
			// This makes importing very convenient :)
			byte[] asnByteArray = Convert.FromBase64String(key);
			privateKey = ImportPrivateKey(asnByteArray);
			publicKey = ImportPublicKey(asnByteArray);
		}

		private static readonly ECKeyGenerationParameters KeyGenParams = new ECKeyGenerationParameters(X9ObjectIdentifiers.Prime256v1, new SecureRandom());

		private static ECPoint ImportPublicKey(byte[] asnByteArray)
		{
			var asnKeyData = (DerSequence)Asn1Object.FromByteArray(asnByteArray);
			var x = (asnKeyData[2] as DerInteger).Value;
			var y = (asnKeyData[3] as DerInteger).Value;

			var ecPoint = KeyGenParams.DomainParameters.Curve.CreatePoint(x, y);
			return ecPoint;
		}

		private static BigInteger ImportPrivateKey(byte[] asnByteArray)
		{
			var asnKeyData = (DerSequence)Asn1Object.FromByteArray(asnByteArray);
			return (asnKeyData[4] as DerInteger).Value;
		}

		private static string ExportPublicKey(ECPoint publicKeyPoint)
		{
			var dataArray = new DerSequence(
					new DerBitString(new byte[] { 0 }, 7),
					new DerInteger(32),
					new DerInteger(publicKeyPoint.AffineXCoord.ToBigInteger()),
					new DerInteger(publicKeyPoint.AffineYCoord.ToBigInteger())).GetDerEncoded();
			return Convert.ToBase64String(dataArray);
		}

		private static string ExportPrivateKey(ECPoint publicKeyPoint, BigInteger privNum)
		{
			var dataArray = new DerSequence(
					new DerBitString(new byte[] { 128 }, 7),
					new DerInteger(32),
					new DerInteger(publicKeyPoint.AffineXCoord.ToBigInteger()),
					new DerInteger(publicKeyPoint.AffineYCoord.ToBigInteger()),
					new DerInteger(privNum)).GetDerEncoded();
			return Convert.ToBase64String(dataArray);
		}

		#endregion

		#region CRYPTO INIT

		/// <summary>Calculates and initializes all required variables for the secure communication.</summary>
		/// <param name="alpha">The alpha key from clientinit encoded in base64.</param>
		/// <param name="beta">The beta key from clientinit encoded in base64.</param>
		/// <param name="omega">The omega key from clientinit encoded in base64.</param>
		public void CryptoInit(string alpha, string beta, string omega)
		{
			if (privateKey == null)
				throw new InvalidOperationException("The private key is not initialized. Use the ImportOwnKeys method before.");

			var alphaBytes = Convert.FromBase64String(alpha);
			var betaBytes = Convert.FromBase64String(beta);
			var omegaBytes = Convert.FromBase64String(omega);
			var serverPublicKey = ImportPublicKey(omegaBytes);

			byte[] sharedKey = GetSharedSecret(serverPublicKey);
			SetSharedSecret(alphaBytes, betaBytes, sharedKey);

			cryptoInitComplete = true;
		}

		/// <summary>Calculates a shared secred with ECDH from the client private and server public key.</summary>
		/// <param name="publicKeyPoint">The public key of the server.</param>
		/// <returns>Returns a 32 byte shared secret.</returns>
		private byte[] GetSharedSecret(ECPoint publicKeyPoint)
		{
			ECPoint p = publicKeyPoint.Multiply(privateKey).Normalize();
			byte[] keyArr = p.AffineXCoord.ToBigInteger().ToByteArray();
			var sharedData = new byte[32];
			Array.Copy(keyArr, keyArr.Length - 32, sharedData, 0, 32);
			return sharedData;
		}

		/// <summary>Initializes all required variables for the secure communication.</summary>
		/// <param name="alpha">The alpha key from clientinit.</param>
		/// <param name="beta">The beta key from clientinit.</param>
		/// <param name="sharedKey">The omega key from clientinit.</param>
		private void SetSharedSecret(byte[] alpha, byte[] beta, byte[] sharedKey)
		{
			// prepares the ivstruct consisting of 2 random byte chains of 10 bytes which each both clients agreed on
			Array.Copy(alpha, 0, ivStruct, 0, 10);
			Array.Copy(beta, 0, ivStruct, 10, 10);

			// applying hashes to get the required values for ts3
			var buffer = Hash1It(sharedKey);
			XorBinary(ivStruct, buffer, 20, ivStruct);

			// creating a dummy signature which will be used on packets which dont use a real encryption signature (like plain voice)
			buffer = Hash1It(ivStruct, 0, 20);
			Array.Copy(buffer, 0, fakeSignature, 0, 8);
		}

		public OutgoingPacket ProcessInit1(int type, byte[] data)
		{
			OutgoingPacket packet = null;
			if (type == -1)
			{
				var sendData = new byte[4 + 1 + 4 + 4 + 8];
				Array.Copy(Initversion, 0, sendData, 0, 4);
				sendData[4] = 0x00;
				for (int i = 0; i < 8; i++) sendData[i + 5] = 0x42; // should be 4byte timestamp + 4byte random

				packet = new OutgoingPacket(sendData, PacketType.Init1)
				{
					UnencryptedFlag = true,
					ClientId = 0,
					PacketId = 101
				};
			}
			else if (type == 1)
			{
				var sendData = new byte[4 + 1 + 16 + 4];
				Array.Copy(Initversion, 0, sendData, 0, 4);
				sendData[4] = 0x02;
				sendData[5] = data[1];
				for (int i = 0; i < 4; i++) sendData[i + 21] = 0x42; // should be second 4byte (random), swapped

				packet = new OutgoingPacket(sendData, PacketType.Init1)
				{
					UnencryptedFlag = true,
					ClientId = 0,
					PacketId = 101
				};
			}
			if (type == 3)
			{
				var sendData = new byte[4 + data.Length + 64];
				Array.Copy(Initversion, 0, sendData, 0, 4);
				sendData[4] = 0x04;
				sendData[5] = data[1];
				for (int i = 0; i < 4; i++) sendData[i + 21] = 0x42; // should be second 4byte (random), swapped

				var exportedPublic = ExportPublicKey(publicKey);
				
				var finalMod = @"clientinitiv alpha=AAAAAAAAAAAAAA== omega=" + exportedPublic + " ip";

				packet = new OutgoingPacket(sendData, PacketType.Init1)
				{
					UnencryptedFlag = true,
					ClientId = 0,
					PacketId = 101
				};
			}
			return packet;
		}

		#endregion

		#region ENCRYPTION/DECRYPTION

		public bool Encrypt(OutgoingPacket packet)
		{
			if (packet.PacketType == PacketType.Init1)
			{
				FakeEncrypt(packet, TS3InitMac);
				return true;
			}
			if (packet.UnencryptedFlag)
			{
				FakeEncrypt(packet, fakeSignature);
				return true;
			}

			var keyNonce = GetKeyNonce(false, packet.PacketId, 0, packet.PacketType);
			packet.BuildHeader();
			ICipherParameters ivAndKey = new AeadParameters(new KeyParameter(keyNonce.Item1), 8 * MacLen, keyNonce.Item2, packet.Header);

			eaxCipher.Init(true, ivAndKey);
			byte[] result = new byte[eaxCipher.GetOutputSize(packet.Size)];
			int len;
			try
			{
				len = eaxCipher.ProcessBytes(packet.Data, 0, packet.Size, result, 0);
				len += eaxCipher.DoFinal(result, len);
			}
			catch (Exception) { return false; }

			// cryptOutArr consists of [Data..., Mac...]
			// to build the final TS3/libtomcrypt we need to copy it into another order

			// len is Data.Length + Mac.Length
			packet.Raw = new byte[OutHeaderLen + len];
			// Copy the Mac from [Data..., Mac...] to [Mac..., Header..., Data...]
			Array.Copy(result, len - MacLen, packet.Raw, 0, MacLen);
			// Copy the Header from packet.Header to [Mac..., Header..., Data...]
			Array.Copy(packet.Header, 0, packet.Raw, MacLen, MacLen);
			// Copy the Data from [Data..., Mac...] to [Mac..., Header..., Data...]
			Array.Copy(result, 0, packet.Raw, MacLen + OutHeaderLen, len);
			// Raw is now [Mac..., Header..., Data...]
			return true;
		}

		private void FakeEncrypt(OutgoingPacket packet, byte[] mac)
		{
			packet.Raw = new byte[packet.Data.Length + MacLen + OutHeaderLen];
			// Copy the Mac from [Data..., Mac...] to [Mac..., Header..., Data...]
			Array.Copy(mac, 0, packet.Raw, 0, MacLen);
			// Copy the Header from this.Header to [Mac..., Header..., Data...]
			Array.Copy(packet.Header, 0, packet.Raw, MacLen, MacLen);
			// Copy the Data from [Data..., Mac...] to [Mac..., Header..., Data...]
			Array.Copy(packet.Data, 0, packet.Raw, MacLen + OutHeaderLen, packet.Data.Length);
			// Raw is now [Mac..., Header..., Data...]
		}

		public IncomingPacket Decrypt(byte[] data)
		{
			if (data.Length < InHeaderLen + MacLen)
				return null;

			var packet = new IncomingPacket(data)
			{
				PacketTypeFlagged = data[MacLen + 2],
				PacketId = (ushort)(data[MacLen] | (data[MacLen + 1] << 8))
			};

			if (packet.PacketType == PacketType.Init1)
			{
				if (!CheckEqual(data, 0, TS3InitMac, 0, MacLen))
					return null;
			}
			else
			{
				if (packet.UnencryptedFlag)
				{
					if (!CheckEqual(data, 0, fakeSignature, 0, MacLen))
						return null;
					FakeDecrypt(packet);
				}
				else
				{
					if (!Decrypt(packet))
						return null;
				}
			}

			return packet;
		}

		private bool Decrypt(IncomingPacket packet)
		{
			Array.Copy(packet.Raw, MacLen, packet.Header, 0, InHeaderLen);
			var keyNonce = GetKeyNonce(false, packet.PacketId, 0, packet.PacketType);
			int dataLen = packet.Raw.Length - (MacLen + InHeaderLen);

			ICipherParameters ivAndKey = new AeadParameters(new KeyParameter(keyNonce.Item1), 8 * MacLen, keyNonce.Item2, packet.Header);
			eaxCipher.Init(false, ivAndKey);
			byte[] result = new byte[eaxCipher.GetOutputSize(dataLen + MacLen)];
			try
			{
				int len = eaxCipher.ProcessBytes(packet.Raw, MacLen + InHeaderLen, dataLen, result, 0);
				len += eaxCipher.ProcessBytes(packet.Raw, 0, MacLen, result, len);
				eaxCipher.DoFinal(result, len);
			}
			catch (Exception) { return false; }
			packet.Data = result;
			return true;
		}

		private static void FakeDecrypt(IncomingPacket packet)
		{
			int dataLen = packet.Raw.Length - (MacLen + InHeaderLen);
			packet.Data = new byte[dataLen];
			Array.Copy(packet.Raw, MacLen + InHeaderLen, packet.Data, 0, dataLen);
		}

		#endregion

		#region OTHER CRYPT FUNCTIONS

		/// <summary>TS3 generates a new key and nonce for each packet sent and received. This method generates and caches these.</summary>
		/// <param name="fromServer">True if the packet is from server to client, false for client to server.</param>
		/// <param name="packetId">The id of the packet, host order.</param>
		/// <param name="generationId">Seriously no idea, just pass 0 and it should be fine.</param>
		/// <param name="packetType">The packetType.</param>
		/// <returns></returns>
		private Tuple<byte[], byte[]> GetKeyNonce(bool fromServer, ushort packetId, uint generationId, PacketType packetType)
		{
			if (!cryptoInitComplete)
				return DummyKeyAndNonceTuple;

			// only the lower 4 bits are used for the real packetType
			byte packetTypeRaw = (byte)packetType;

			int cacheIndex = packetTypeRaw * (fromServer ? 1 : 2);
			if (cachedKeyNonces[cacheIndex] == null)
			{
				// this part of the key/nonce is fixed by the message direction and packetType
				byte[] tmpToHash = new byte[26];

				if (fromServer)
					tmpToHash[0] = 0x30;
				else
					tmpToHash[0] = 0x31;

				tmpToHash[1] = packetTypeRaw;

				Array.Copy(BitConverter.GetBytes(NetUtil.H2N(generationId)), 0, tmpToHash, 2, 4);
				Array.Copy(ivStruct, 0, tmpToHash, 6, 20);

				var result = Hash256It(tmpToHash);

				cachedKeyNonces[cacheIndex] = new Tuple<byte[], byte[]>(result.Slice(0, 16).ToArray(), result.Slice(16, 16).ToArray());
			}

			byte[] key = new byte[16];
			byte[] nonce = new byte[16];
			Array.Copy(cachedKeyNonces[cacheIndex].Item1, 0, key, 0, 16);
			Array.Copy(cachedKeyNonces[cacheIndex].Item2, 0, nonce, 0, 16);

			// finally the first two bytes get xor'd with the packet id
			// TODO: this could be written more efficiently
			var startData = NetUtil.N2H(BitConverter.ToUInt16(key, 0));
			startData = (ushort)(startData ^ packetId);
			var xordata = BitConverter.GetBytes(NetUtil.H2N(startData));
			Array.Copy(xordata, 0, key, 0, xordata.Length);

			return new Tuple<byte[], byte[]>(key, nonce);
		}

		/// <summary>This method calculates x ^ (2^level) % n = y which is the solution to the server RSA puzzle.</summary>
		/// <param name="x">The x number, unsigned, as a bytearray from BigInteger. x is the base.</param>
		/// <param name="n">The n number, unsigned, as a bytearray from BigInteger. n is the modulus.</param>
		/// <param name="level"></param>
		/// <returns>The y value, unsigned, as a bytearray from BigInteger</returns>
		private static byte[] SolveRsaChallange(byte[] x, byte[] n, int level = 10000)
		{
			if (x == null || n == null) return null;
			var bign = new BigInteger(1, n);
			var bigx = new BigInteger(1, x);
			return bigx.ModPow(BigInteger.Two.Pow(level), bign).ToByteArrayUnsigned();
		}

		#endregion

		#region CRYPT HELPER

		static bool CheckEqual(byte[] a1, int a1Index, byte[] a2, int a2Index, int len)
		{
			for (int i = 0; i < len; i++)
				if (a1[i + a1Index] != a2[i + a2Index]) return false;
			return true;
		}

		public static void XorBinary(byte[] a, byte[] b, int len, byte[] outBuf)
		{
			if (a.Length < len || b.Length < len || outBuf.Length < len) throw new ArgumentException();
			for (int i = 0; i < len; i++)
				outBuf[i] = (byte)(a[i] ^ b[i]);
		}

		private static readonly Sha1Digest Sha1Hash = new Sha1Digest();
		private static readonly Sha256Digest Sha265Hash = new Sha256Digest();
		private static byte[] Hash1It(byte[] data, int offset = 0, int len = 0) => HashIt(Sha1Hash, data, offset, len);
		private static byte[] Hash256It(byte[] data, int offset = 0, int len = 0) => HashIt(Sha265Hash, data, offset, len);
		private static byte[] HashIt(GeneralDigest hashAlgo, byte[] data, int offset = 0, int len = 0)
		{
			byte[] result;
			lock (hashAlgo)
			{
				hashAlgo.Reset();
				hashAlgo.BlockUpdate(data, offset, len == 0 ? data.Length - offset : len);
				result = new byte[hashAlgo.GetDigestSize()];
				hashAlgo.DoFinal(result, 0);
			}
			return result;
		}

		#endregion
	}
}
