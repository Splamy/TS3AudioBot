namespace TS3Client.Full
{
	using Org.BouncyCastle.Math.EC;
	using Org.BouncyCastle.Math;

	public class IdentityData
	{
		public string PublicKeyString { get; set; }
		public string PrivateKeyString { get; set; }
		public ECPoint PublicKey { get; set; }
		public BigInteger PrivateKey { get; set; }
		public ulong ValidKeyOffset { get; set; }
		public ulong LastCheckedKeyOffset { get; set; }
	}
}
