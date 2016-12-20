namespace TS3Client.Full
{
    using Org.BouncyCastle.Math.EC;
    using Org.BouncyCastle.Math;
    using Newtonsoft.Json;

    public class IdentityContainer
    {
        public IdentityContainer() { }
        public IdentityContainer(IdentityData Input)
        {
            privateKeyString = Input.PrivateKeyString;
            ValidKeyOffset = Input.ValidKeyOffset;
            lastCheckedKeyOffset = Input.LastCheckedKeyOffset;
        }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
        public string privateKeyString { get; set; }
        public System.UInt64 ValidKeyOffset { get; set; }
        public System.UInt64 lastCheckedKeyOffset { get; set; }
    }
    public class IdentityData
    {
        public string PublicKeyString { get; set; }
        public string PrivateKeyString { get; set; }
        public ECPoint PublicKey { get; set; }
        public BigInteger PrivateKey { get; set; }
        public ulong ValidKeyOffset { get; set; }
        public ulong LastCheckedKeyOffset { get; set; }
        public override string ToString()
        {
            return new IdentityContainer(this).ToString();
        }
    }
}
