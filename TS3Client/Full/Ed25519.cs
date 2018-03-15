/* Ported and refactored from Java to C# by Hans Wolff, 10/10/2013
 * Released to the public domain
 * /

/* Java code written by k3d3
 * Source: https://github.com/k3d3/ed25519-java/blob/master/ed25519.java
 * Released to the public domain
 */

// Code from: https://github.com/hanswolff/ed25519

namespace TS3Client.Full
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Numerics;
	using System.Security.Cryptography;
	using System.Globalization;

	public static class Ed25519
	{
		private static byte[] ComputeHash(byte[] m)
		{
			using (var sha512 = SHA512.Create())
			{
				return sha512.ComputeHash(m);
			}
		}

		private static BigInteger ExpMod(BigInteger number, BigInteger exponent, BigInteger modulo)
		{
			if (exponent.Equals(BigInteger.Zero))
			{
				return BigInteger.One;
			}
			BigInteger t = BigInteger.Pow(ExpMod(number, exponent / Two, modulo), 2).Mod(modulo);
			if (!exponent.IsEven)
			{
				t *= number;
				t = t.Mod(modulo);
			}
			return t;
		}

		private static BigInteger Inv(BigInteger x)
		{
			return ExpMod(x, Qm2, Q);
		}

		private static BigInteger RecoverX(BigInteger y)
		{
			BigInteger y2 = y * y;
			var inv = Inv(D * y2 + 1);
			BigInteger xx = (y2 - 1) * inv;
			BigInteger x = ExpMod(xx, Qp3 / Eight, Q);
			if (!(x * x - xx).Mod(Q).Equals(BigInteger.Zero))
			{
				x = (x * I).Mod(Q);
			}
			if (!x.IsEven)
			{
				x = Q - x;
			}
			return x;
		}

		// This is a goddamn Add(p, q) function
		public static (BigInteger x, BigInteger y) Edwards(BigInteger px, BigInteger py, BigInteger qx, BigInteger qy)
		{
			BigInteger xx12 = px * qx;
			BigInteger yy12 = py * qy;
			BigInteger dtemp = D * xx12 * yy12;
			BigInteger x3 = (px * qy + qx * py) * (Inv(1 + dtemp));
			BigInteger y3 = (py * qy + xx12) * (Inv(1 - dtemp));
			return (x3.Mod(Q), y3.Mod(Q));
		}

		private static (BigInteger x, BigInteger y) EdwardsSquare(BigInteger x, BigInteger y)
		{
			BigInteger xx = x * x;
			BigInteger yy = y * y;
			BigInteger dtemp = D * xx * yy;
			BigInteger x3 = (2 * x * y) * (Inv(1 + dtemp));
			BigInteger y3 = (yy + xx) * (Inv(1 - dtemp));
			return (x3.Mod(Q), y3.Mod(Q));
		}

		public static (BigInteger x, BigInteger y) ScalarMul((BigInteger x, BigInteger y) p, BigInteger e)
		{
			if (e.Equals(BigInteger.Zero))
			{
				return (BigInteger.Zero, BigInteger.One);
			}
			var q = ScalarMul(p, e / Two);
			q = EdwardsSquare(q.x, q.y);
			if (!e.IsEven) q = Edwards(q.x, q.y, p.x, p.y);
			return q;
		}

		public static byte[] EncodeInt(this BigInteger y)
		{
			byte[] nin = y.ToByteArray();
			var nout = new byte[Math.Max(nin.Length, 32)];
			Array.Copy(nin, nout, nin.Length);
			return nout;
		}

		public static byte[] EncodePoint(BigInteger x, BigInteger y)
		{
			byte[] nout = EncodeInt(y);
			nout[nout.Length - 1] |= (x.IsEven ? (byte)0 : (byte)0x80);
			return nout;
		}

		private static int GetBit(byte[] h, int i)
		{
			return h[i / 8] >> (i % 8) & 1;
		}

		public static byte[] PublicKey(byte[] signingKey)
		{
			byte[] h = ComputeHash(signingKey);
			BigInteger a = TwoPowBitLengthMinusTwo;
			for (int i = 3; i < (BitLength - 2); i++)
			{
				var bit = GetBit(h, i);
				if (bit != 0)
				{
					a += TwoPowCache[i];
				}
			}
			var (x, y) = ScalarMul(B, a);
			return EncodePoint(x, y);
		}

		private static BigInteger HashInt(byte[] m)
		{
			byte[] h = ComputeHash(m);
			BigInteger hsum = BigInteger.Zero;
			for (int i = 0; i < 2 * BitLength; i++)
			{
				var bit = GetBit(h, i);
				if (bit != 0)
				{
					hsum += TwoPowCache[i];
				}
			}
			return hsum;
		}

		public static byte[] Signature(byte[] message, byte[] signingKey, byte[] publicKey)
		{
			byte[] h = ComputeHash(signingKey);
			BigInteger a = TwoPowBitLengthMinusTwo;
			for (int i = 3; i < (BitLength - 2); i++)
			{
				var bit = GetBit(h, i);
				if (bit != 0)
				{
					a += TwoPowCache[i];
				}
			}

			BigInteger r;
			using (var rsub = new MemoryStream((BitLength / 8) + message.Length))
			{
				rsub.Write(h, BitLength / 8, BitLength / 4 - BitLength / 8);
				rsub.Write(message, 0, message.Length);
				r = HashInt(rsub.ToArray());
			}
			var (x, y) = ScalarMul(B, r);
			BigInteger s;
			var encodedBigR = EncodePoint(x, y);
			using (var stemp = new MemoryStream(32 + publicKey.Length + message.Length))
			{
				stemp.Write(encodedBigR, 0, encodedBigR.Length);
				stemp.Write(publicKey, 0, publicKey.Length);
				stemp.Write(message, 0, message.Length);
				s = (r + HashInt(stemp.ToArray()) * a).Mod(L);
			}

			using (var nout = new MemoryStream(64))
			{
				nout.Write(encodedBigR, 0, encodedBigR.Length);
				var encodeInt = EncodeInt(s);
				nout.Write(encodeInt, 0, encodeInt.Length);
				return nout.ToArray();
			}
		}

		private static bool IsOnCurve(BigInteger x, BigInteger y)
		{
			BigInteger xx = x * x;
			BigInteger yy = y * y;
			BigInteger dxxyy = D * yy * xx;
			return (yy - xx - dxxyy - 1).Mod(Q).Equals(BigInteger.Zero);
		}

		public static BigInteger DecodeInt(byte[] s)
		{
			return new BigInteger(s) & Un;
		}

		public static BigInteger DecodeIntMod(byte[] s)
		{
			var dec = DecodeInt(s);
			var mod = dec % L;
			return mod;
		}

		public static (BigInteger x, BigInteger y) DecodePoint(byte[] pointBytes)
		{
			BigInteger y = DecodeInt(pointBytes);
			BigInteger x = RecoverX(y);
			if ((x.IsEven ? 0 : 1) != GetBit(pointBytes, BitLength - 1))
			{
				x = Q - x;
			}
			var point = (x, y);
			if (!IsOnCurve(x, y)) throw new ArgumentException("Decoding point that is not on curve");
			return point;
		}

		public static bool CheckValid(byte[] signature, byte[] message, byte[] publicKey)
		{
			if (signature.Length != BitLength / 4) throw new ArgumentException("Signature length is wrong");
			if (publicKey.Length != BitLength / 8) throw new ArgumentException("Public key length is wrong");

			byte[] rByte = Arrays.CopyOfRange(signature, 0, BitLength / 8);
			var (x, y) = DecodePoint(rByte);
			var a = DecodePoint(publicKey);

			byte[] sByte = Arrays.CopyOfRange(signature, BitLength / 8, BitLength / 4);
			BigInteger s = DecodeInt(sByte);
			BigInteger h;

			using (var stemp = new MemoryStream(32 + publicKey.Length + message.Length))
			{
				var encodePoint = EncodePoint(x, y);
				stemp.Write(encodePoint, 0, encodePoint.Length);
				stemp.Write(publicKey, 0, publicKey.Length);
				stemp.Write(message, 0, message.Length);
				h = HashInt(stemp.ToArray());
			}
			var ra = ScalarMul(B, s);
			var ah = ScalarMul(a, h);
			var rb = Edwards(x, y, ah.x, ah.y);
			if (!ra.x.Equals(rb.x) || !ra.y.Equals(rb.y))
				return false;
			return true;
		}

		public static (byte[] privateKey, byte[] publicKey) CreateKeyPair()
		{
			var privateKey = CreatePrivateKey();
			var publicKey = CreatePublicKey(privateKey);
			return (privateKey, publicKey.Encode());
		}

		public static byte[] CreatePrivateKey()
		{
			var privateBuffer = new byte[32];
			using (var rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(privateBuffer);
			}
			return privateBuffer;
		}

		public static (BigInteger, BigInteger) CreatePublicKey(byte[] privateKey)
		{
			var privInt = DecodeInt(privateKey);
			return ScalarMul(B, privInt);
		}

		private const int BitLength = 256;

		private static readonly BigInteger TwoPowBitLengthMinusTwo = BigInteger.Pow(2, BitLength - 2);
		private static readonly BigInteger[] TwoPowCache = Enumerable.Range(0, 2 * BitLength).Select(i => BigInteger.Pow(2, i)).ToArray();

		private static readonly BigInteger Q =
			BigInteger.Parse("57896044618658097711785492504343953926634992332820282019728792003956564819949");

		private static readonly BigInteger Qm2 =
			BigInteger.Parse("57896044618658097711785492504343953926634992332820282019728792003956564819947");

		private static readonly BigInteger Qp3 =
			BigInteger.Parse("57896044618658097711785492504343953926634992332820282019728792003956564819952");

		private static readonly BigInteger L =
			BigInteger.Parse("7237005577332262213973186563042994240857116359379907606001950938285454250989");

		private static readonly BigInteger D =
			BigInteger.Parse("-4513249062541557337682894930092624173785641285191125241628941591882900924598840740");

		private static readonly BigInteger I =
			BigInteger.Parse("19681161376707505956807079304988542015446066515923890162744021073123829784752");

		private static readonly BigInteger By =
			BigInteger.Parse("46316835694926478169428394003475163141307993866256225615783033603165251855960");

		// base point
		private static readonly BigInteger Bx =
			BigInteger.Parse("15112221349535400772501151409588531511454012693041857206046113283949847762202");

		private static readonly (BigInteger, BigInteger) B = (Bx.Mod(Q), By.Mod(Q));

		private static readonly BigInteger Un =
			BigInteger.Parse("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", NumberStyles.AllowHexSpecifier);

		private static readonly BigInteger Two = new BigInteger(2);
		private static readonly BigInteger Eight = new BigInteger(8);

		public static BigInteger ToNet(this Org.BouncyCastle.Math.BigInteger num)
		{
			return BigInteger.Parse(num.ToString());
		}

		public static Org.BouncyCastle.Math.BigInteger ToBc(this BigInteger num)
		{
			return new Org.BouncyCastle.Math.BigInteger(num.ToString());
		}

		public static (BigInteger x, BigInteger y) ToNet(this Org.BouncyCastle.Math.EC.ECPoint ecp)
		{
			return (ecp.XCoord.ToBigInteger().ToNet(), ecp.YCoord.ToBigInteger().ToNet());
		}

		public static Org.BouncyCastle.Math.EC.ECPoint ToBc(this (BigInteger x, BigInteger y) ecp)
		{
			return Ts3Crypt.Ed25519Curve.Curve.CreatePoint(ecp.x.ToBc(), ecp.y.ToBc());
		}

		public static byte[] Encode(this (BigInteger x, BigInteger y) ecp)
		{
			return EncodePoint(ecp.x, ecp.y);
		}
	}

	internal static class Arrays
	{
		public static byte[] CopyOfRange(byte[] original, int from, int to)
		{
			int length = to - from;
			var result = new byte[length];
			Array.Copy(original, from, result, 0, length);
			return result;
		}
	}

	internal static class BigIntegerHelpers
	{
		public static BigInteger Mod(this BigInteger num, BigInteger modulo)
		{
			var result = num % modulo;
			return result < 0 ? result + modulo : result;
		}
	}
}
