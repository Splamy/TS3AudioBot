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
			BigInteger xx = (y2 - 1) * Inv(D * y2 + 1);
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

		private static Tuple<BigInteger, BigInteger> Edwards(BigInteger px, BigInteger py, BigInteger qx, BigInteger qy)
		{
			BigInteger xx12 = px * qx;
			BigInteger yy12 = py * qy;
			BigInteger dtemp = D * xx12 * yy12;
			BigInteger x3 = (px * qy + qx * py) * (Inv(1 + dtemp));
			BigInteger y3 = (py * qy + xx12) * (Inv(1 - dtemp));
			return new Tuple<BigInteger, BigInteger>(x3.Mod(Q), y3.Mod(Q));
		}

		private static Tuple<BigInteger, BigInteger> EdwardsSquare(BigInteger x, BigInteger y)
		{
			BigInteger xx = x * x;
			BigInteger yy = y * y;
			BigInteger dtemp = D * xx * yy;
			BigInteger x3 = (2 * x * y) * (Inv(1 + dtemp));
			BigInteger y3 = (yy + xx) * (Inv(1 - dtemp));
			return new Tuple<BigInteger, BigInteger>(x3.Mod(Q), y3.Mod(Q));
		}
		private static Tuple<BigInteger, BigInteger> ScalarMul(Tuple<BigInteger, BigInteger> p, BigInteger e)
		{
			if (e.Equals(BigInteger.Zero))
			{
				return new Tuple<BigInteger, BigInteger>(BigInteger.Zero, BigInteger.One);
			}
			var q = ScalarMul(p, e / Two);
			q = EdwardsSquare(q.Item1, q.Item2);
			if (!e.IsEven) q = Edwards(q.Item1, q.Item2, p.Item1, p.Item2);
			return q;
		}

		public static byte[] EncodeInt(BigInteger y)
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
			var bigA = ScalarMul(B, a);
			return EncodePoint(bigA.Item1, bigA.Item2);
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
			var bigR = ScalarMul(B, r);
			BigInteger s;
			var encodedBigR = EncodePoint(bigR.Item1, bigR.Item2);
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

		public static Tuple<BigInteger, BigInteger> DecodePoint(byte[] pointBytes)
		{
			BigInteger y = new BigInteger(pointBytes) & Un;
			BigInteger x = RecoverX(y);
			if ((x.IsEven ? 0 : 1) != GetBit(pointBytes, BitLength - 1))
			{
				x = Q - x;
			}
			var point = new Tuple<BigInteger, BigInteger>(x, y);
			if (!IsOnCurve(x, y)) throw new ArgumentException("Decoding point that is not on curve");
			return point;
		}

		public static bool CheckValid(byte[] signature, byte[] message, byte[] publicKey)
		{
			if (signature.Length != BitLength / 4) throw new ArgumentException("Signature length is wrong");
			if (publicKey.Length != BitLength / 8) throw new ArgumentException("Public key length is wrong");

			byte[] rByte = Arrays.CopyOfRange(signature, 0, BitLength / 8);
			var r = DecodePoint(rByte);
			var a = DecodePoint(publicKey);

			byte[] sByte = Arrays.CopyOfRange(signature, BitLength / 8, BitLength / 4);
			BigInteger s = DecodeInt(sByte);
			BigInteger h;

			using (var stemp = new MemoryStream(32 + publicKey.Length + message.Length))
			{
				var encodePoint = EncodePoint(r.Item1, r.Item2);
				stemp.Write(encodePoint, 0, encodePoint.Length);
				stemp.Write(publicKey, 0, publicKey.Length);
				stemp.Write(message, 0, message.Length);
				h = HashInt(stemp.ToArray());
			}
			var ra = ScalarMul(B, s);
			var ah = ScalarMul(a, h);
			var rb = Edwards(r.Item1, r.Item2, ah.Item1, ah.Item2);
			if (!ra.Item1.Equals(rb.Item1) || !ra.Item2.Equals(rb.Item2))
				return false;
			return true;
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

		private static readonly BigInteger Bx =
			BigInteger.Parse("15112221349535400772501151409588531511454012693041857206046113283949847762202");

		private static readonly Tuple<BigInteger, BigInteger> B = new Tuple<BigInteger, BigInteger>(Bx.Mod(Q), By.Mod(Q));

		private static readonly BigInteger Un =
			BigInteger.Parse("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", NumberStyles.AllowHexSpecifier);

		private static readonly BigInteger Two = new BigInteger(2);
		private static readonly BigInteger Eight = new BigInteger(8);

		public static BigInteger ToNetBi(this Org.BouncyCastle.Math.BigInteger num)
		{
			return BigInteger.Parse(num.ToString());
		}

		public static Org.BouncyCastle.Math.BigInteger ToBcBi(this BigInteger num)
		{
			return new Org.BouncyCastle.Math.BigInteger(num.ToString());
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
