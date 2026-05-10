using System.Text;
using TSLib.Full;

namespace TSLib.Tests;

public class TsCryptTests
{
	private const int CryptVariant1 = 1;
	private const int CryptVariant2 = 2;

	private static readonly IdentityData ServerIdentity =
		IdentityData.FromBase64("MCoDAgbAAgEgAiEA/nlJni5tVdlScGN7aNXC5c5W8wqv4dNYvyJWYa9H1qo=", 254).Unwrap();

	private static readonly IdentityData ClientIdentity =
		IdentityData.FromBase64("MCkDAgbAAgEgAiAB3eWIaInIGNH+tcOtwqxiwORAi/z40X/v4y5Pw0beYw==", 292).Unwrap();

	private const string TestV1Alpha = "b2Gq47nxKxedew==";
	private const string TestV1Beta = "i6gTG34SOLIDxQ==";
	private static readonly string TestV1Omega = ServerIdentity.PublicKeyString;

	private const string TestV2Licence =
		"AQBgjAAqtcBUrw5futTtkl3+EM3OW4Lal6OTPlwuv4xV/gIRFlEAG0NlAAcAAAAgQW5vbnltb3VzAABvA5f0Q1mAXmx1oc0cFwjlZ7QlMgo5p589HeNf6riI2SAZHOYdGR2O3Q==";

	private static readonly string Test2VOmega = ServerIdentity.PublicKeyString;

	private static readonly string TestV2Proof =
		Convert.ToBase64String(TsCrypt.Sign(ServerIdentity.PrivateKey, Convert.FromBase64String(TestV2Licence)));

	private const string TestV2Beta = "0q4jbTKHp5laNl57E6MXGefRwVbT5vBMC43yxaIT5G0pLFLfZcQGsw+GXAwZ3INtmYR+X+7E";

	private const string TestV2PrivateKey = "sPK5RnpkDkGZDU9rqRQy34U9W9F63zL7WbVf5hxfp1Y=";
	private const string TestV2PublicKey = "WdYu9oroBgTpiVzyrFo4QQLtK+kluudg3PfW7YhryG8=";

	[Theory]
	[InlineData(1, false, 0, 0, PacketType.Init1, false, "8jQziYv/VSiRpvUqG2wRjA==", "aGM/h/7b088yrvPmBC0JTg==")]
	[InlineData(1, false, 0, 0, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 0, 0, PacketType.Command, false, "/w53jR9o5tbDV3bO1LDLow==", "m5rYrwxgr+4lAqRUJODaDw==")]
	[InlineData(1, false, 0, 0, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 0, 0, PacketType.Voice, false, "79fc7aMyhSKvfVrI6rQ7aw==", "5BiNTd8bMqHhvX2Lz7wplQ==")]
	[InlineData(1, false, 0, 0, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 0, 1, PacketType.Init1, false, "vO5M+NTUyNEhWVtl7Ir1Ng==", "0EJYQnG2wnWmJMHQ7zdB8g==")]
	[InlineData(1, false, 0, 1, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 0, 1, PacketType.Command, false, "oVmqxTs2U9PcXki0CLAkaw==", "64704BHOUjv2OXnZmsjUtw==")]
	[InlineData(1, false, 0, 1, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 0, 1, PacketType.Voice, false, "FaM9DISRawRYFQsjaxvQHQ==", "sMMksDNhxh+VcEBY9X0lug==")]
	[InlineData(1, false, 0, 1, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 0, 100, PacketType.Init1, false, "+XO2Z8QtX1YDntrNX5ToEQ==", "1Ap8QbgCvnrH69just6GGQ==")]
	[InlineData(1, false, 0, 100, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 0, 100, PacketType.Command, false, "jc47rVUBc8lZAuRG0QZXnQ==", "4FR2OYRRTYQ6Rbe8nArY4A==")]
	[InlineData(1, false, 0, 100, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 0, 100, PacketType.Voice, false, "YqP8iTCGvmEsPyvoC6Ym+g==", "1Xz+IO2RZLz7yP6f5RTdMQ==")]
	[InlineData(1, false, 0, 100, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 1, 0, PacketType.Init1, false, "8jUziYv/VSiRpvUqG2wRjA==", "aGM/h/7b088yrvPmBC0JTg==")]
	[InlineData(1, false, 1, 0, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 1, 0, PacketType.Command, false, "/w93jR9o5tbDV3bO1LDLow==", "m5rYrwxgr+4lAqRUJODaDw==")]
	[InlineData(1, false, 1, 0, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 1, 0, PacketType.Voice, false, "79bc7aMyhSKvfVrI6rQ7aw==", "5BiNTd8bMqHhvX2Lz7wplQ==")]
	[InlineData(1, false, 1, 0, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 1, 1, PacketType.Init1, false, "vO9M+NTUyNEhWVtl7Ir1Ng==", "0EJYQnG2wnWmJMHQ7zdB8g==")]
	[InlineData(1, false, 1, 1, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 1, 1, PacketType.Command, false, "oViqxTs2U9PcXki0CLAkaw==", "64704BHOUjv2OXnZmsjUtw==")]
	[InlineData(1, false, 1, 1, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 1, 1, PacketType.Voice, false, "FaI9DISRawRYFQsjaxvQHQ==", "sMMksDNhxh+VcEBY9X0lug==")]
	[InlineData(1, false, 1, 1, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 1, 100, PacketType.Init1, false, "+XK2Z8QtX1YDntrNX5ToEQ==", "1Ap8QbgCvnrH69just6GGQ==")]
	[InlineData(1, false, 1, 100, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 1, 100, PacketType.Command, false, "jc87rVUBc8lZAuRG0QZXnQ==", "4FR2OYRRTYQ6Rbe8nArY4A==")]
	[InlineData(1, false, 1, 100, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 1, 100, PacketType.Voice, false, "YqL8iTCGvmEsPyvoC6Ym+g==", "1Xz+IO2RZLz7yP6f5RTdMQ==")]
	[InlineData(1, false, 1, 100, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 100, 0, PacketType.Init1, false, "8lAziYv/VSiRpvUqG2wRjA==", "aGM/h/7b088yrvPmBC0JTg==")]
	[InlineData(1, false, 100, 0, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 100, 0, PacketType.Command, false, "/2p3jR9o5tbDV3bO1LDLow==", "m5rYrwxgr+4lAqRUJODaDw==")]
	[InlineData(1, false, 100, 0, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 100, 0, PacketType.Voice, false, "77Pc7aMyhSKvfVrI6rQ7aw==", "5BiNTd8bMqHhvX2Lz7wplQ==")]
	[InlineData(1, false, 100, 0, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 100, 1, PacketType.Init1, false, "vIpM+NTUyNEhWVtl7Ir1Ng==", "0EJYQnG2wnWmJMHQ7zdB8g==")]
	[InlineData(1, false, 100, 1, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 100, 1, PacketType.Command, false, "oT2qxTs2U9PcXki0CLAkaw==", "64704BHOUjv2OXnZmsjUtw==")]
	[InlineData(1, false, 100, 1, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 100, 1, PacketType.Voice, false, "Fcc9DISRawRYFQsjaxvQHQ==", "sMMksDNhxh+VcEBY9X0lug==")]
	[InlineData(1, false, 100, 1, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 100, 100, PacketType.Init1, false, "+Re2Z8QtX1YDntrNX5ToEQ==", "1Ap8QbgCvnrH69just6GGQ==")]
	[InlineData(1, false, 100, 100, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 100, 100, PacketType.Command, false, "jao7rVUBc8lZAuRG0QZXnQ==", "4FR2OYRRTYQ6Rbe8nArY4A==")]
	[InlineData(1, false, 100, 100, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, false, 100, 100, PacketType.Voice, false, "Ysf8iTCGvmEsPyvoC6Ym+g==", "1Xz+IO2RZLz7yP6f5RTdMQ==")]
	[InlineData(1, false, 100, 100, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 0, 0, PacketType.Init1, false, "MqyJwfk1kbk6px0njFJ8EA==", "Dx5KYcsd5jLE4gMp1LB6Ng==")]
	[InlineData(1, true, 0, 0, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 0, 0, PacketType.Command, false, "HV+uMbzxDG+Tm/5DeErNuw==", "TWZzR9x67mnuvsIwZ82QCQ==")]
	[InlineData(1, true, 0, 0, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 0, 0, PacketType.Voice, false, "AzrpopMSwNlghYjKy5eJVg==", "s5Gg5IO1udbXQFSGeF6z1A==")]
	[InlineData(1, true, 0, 0, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 0, 1, PacketType.Init1, false, "pvx+Dzg+xJiGMf9ePbzkMA==", "jJFjK2gdWBQssgzM8h7+fA==")]
	[InlineData(1, true, 0, 1, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 0, 1, PacketType.Command, false, "I/Gt8iFGYGsZt2uD2JHUGw==", "34YK0dwqnNF+cqigG9zmnQ==")]
	[InlineData(1, true, 0, 1, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 0, 1, PacketType.Voice, false, "Diwwc1o//LIlmv7hk8aUIQ==", "IuDfV/OKp0oaPGmvHN2aJA==")]
	[InlineData(1, true, 0, 1, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 0, 100, PacketType.Init1, false, "gqhpGsPNZRug1eAu7XTRtg==", "k+4/5I6QNzWnygcUI0HCjg==")]
	[InlineData(1, true, 0, 100, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 0, 100, PacketType.Command, false, "GklpHq/k9K5D8oHsUpX3AQ==", "5QZsn4kKg5AqNdIZVoXgdA==")]
	[InlineData(1, true, 0, 100, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 0, 100, PacketType.Voice, false, "730GeelIYt6J6hxCuQD41g==", "YEFEMZWLgYSoXUGogeHJPw==")]
	[InlineData(1, true, 0, 100, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 1, 0, PacketType.Init1, false, "Mq2Jwfk1kbk6px0njFJ8EA==", "Dx5KYcsd5jLE4gMp1LB6Ng==")]
	[InlineData(1, true, 1, 0, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 1, 0, PacketType.Command, false, "HV6uMbzxDG+Tm/5DeErNuw==", "TWZzR9x67mnuvsIwZ82QCQ==")]
	[InlineData(1, true, 1, 0, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 1, 0, PacketType.Voice, false, "AzvpopMSwNlghYjKy5eJVg==", "s5Gg5IO1udbXQFSGeF6z1A==")]
	[InlineData(1, true, 1, 0, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 1, 1, PacketType.Init1, false, "pv1+Dzg+xJiGMf9ePbzkMA==", "jJFjK2gdWBQssgzM8h7+fA==")]
	[InlineData(1, true, 1, 1, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 1, 1, PacketType.Command, false, "I/Ct8iFGYGsZt2uD2JHUGw==", "34YK0dwqnNF+cqigG9zmnQ==")]
	[InlineData(1, true, 1, 1, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 1, 1, PacketType.Voice, false, "Di0wc1o//LIlmv7hk8aUIQ==", "IuDfV/OKp0oaPGmvHN2aJA==")]
	[InlineData(1, true, 1, 1, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 1, 100, PacketType.Init1, false, "gqlpGsPNZRug1eAu7XTRtg==", "k+4/5I6QNzWnygcUI0HCjg==")]
	[InlineData(1, true, 1, 100, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 1, 100, PacketType.Command, false, "GkhpHq/k9K5D8oHsUpX3AQ==", "5QZsn4kKg5AqNdIZVoXgdA==")]
	[InlineData(1, true, 1, 100, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 1, 100, PacketType.Voice, false, "73wGeelIYt6J6hxCuQD41g==", "YEFEMZWLgYSoXUGogeHJPw==")]
	[InlineData(1, true, 1, 100, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 100, 0, PacketType.Init1, false, "MsiJwfk1kbk6px0njFJ8EA==", "Dx5KYcsd5jLE4gMp1LB6Ng==")]
	[InlineData(1, true, 100, 0, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 100, 0, PacketType.Command, false, "HTuuMbzxDG+Tm/5DeErNuw==", "TWZzR9x67mnuvsIwZ82QCQ==")]
	[InlineData(1, true, 100, 0, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 100, 0, PacketType.Voice, false, "A17popMSwNlghYjKy5eJVg==", "s5Gg5IO1udbXQFSGeF6z1A==")]
	[InlineData(1, true, 100, 0, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 100, 1, PacketType.Init1, false, "pph+Dzg+xJiGMf9ePbzkMA==", "jJFjK2gdWBQssgzM8h7+fA==")]
	[InlineData(1, true, 100, 1, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 100, 1, PacketType.Command, false, "I5Wt8iFGYGsZt2uD2JHUGw==", "34YK0dwqnNF+cqigG9zmnQ==")]
	[InlineData(1, true, 100, 1, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 100, 1, PacketType.Voice, false, "Dkgwc1o//LIlmv7hk8aUIQ==", "IuDfV/OKp0oaPGmvHN2aJA==")]
	[InlineData(1, true, 100, 1, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 100, 100, PacketType.Init1, false, "gsxpGsPNZRug1eAu7XTRtg==", "k+4/5I6QNzWnygcUI0HCjg==")]
	[InlineData(1, true, 100, 100, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 100, 100, PacketType.Command, false, "Gi1pHq/k9K5D8oHsUpX3AQ==", "5QZsn4kKg5AqNdIZVoXgdA==")]
	[InlineData(1, true, 100, 100, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(1, true, 100, 100, PacketType.Voice, false, "7xkGeelIYt6J6hxCuQD41g==", "YEFEMZWLgYSoXUGogeHJPw==")]
	[InlineData(1, true, 100, 100, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 0, 0, PacketType.Init1, false, "+lqQHVFrFa5DW4uPyrPRGQ==", "jRigPizU7CcMtPR+yEe2vQ==")]
	[InlineData(2, false, 0, 0, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 0, 0, PacketType.Command, false, "lPy2lalci8isGX7CxjStdA==", "KIwyOcMc9sTEmHXqqmOpzg==")]
	[InlineData(2, false, 0, 0, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 0, 0, PacketType.Voice, false, "c4WipY++i4VpttAWz7+v0g==", "VqGq1TKYNMDvKazUMa3WwQ==")]
	[InlineData(2, false, 0, 0, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 0, 1, PacketType.Init1, false, "OgjCOC//82RGgM+o6rX6DA==", "Q0hY8qaYejnTDKqB8Y8CQw==")]
	[InlineData(2, false, 0, 1, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 0, 1, PacketType.Command, false, "mJ11RVbXoYnQpzBMpKI5eQ==", "9oAWGsFfG/q4EdJ5IoQeOQ==")]
	[InlineData(2, false, 0, 1, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 0, 1, PacketType.Voice, false, "3En/s2ysA1M471os+Iqe/w==", "+Cpq45iH27J7Q0i16F73Yg==")]
	[InlineData(2, false, 0, 1, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 0, 100, PacketType.Init1, false, "rxAdvWJC8EBMg1sA7r4hBg==", "4Dlr/7alaRBrC24YEh9Gzg==")]
	[InlineData(2, false, 0, 100, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 0, 100, PacketType.Command, false, "IcdnzSA2KpGMWLVk/MuHLw==", "rsWaLk37mje+yntPPPfhLA==")]
	[InlineData(2, false, 0, 100, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 0, 100, PacketType.Voice, false, "tpb0eYTzixmFLXXG4zZndg==", "JKKtnvzl0omFP1QCGuQbRw==")]
	[InlineData(2, false, 0, 100, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 1, 0, PacketType.Init1, false, "+luQHVFrFa5DW4uPyrPRGQ==", "jRigPizU7CcMtPR+yEe2vQ==")]
	[InlineData(2, false, 1, 0, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 1, 0, PacketType.Command, false, "lP22lalci8isGX7CxjStdA==", "KIwyOcMc9sTEmHXqqmOpzg==")]
	[InlineData(2, false, 1, 0, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 1, 0, PacketType.Voice, false, "c4SipY++i4VpttAWz7+v0g==", "VqGq1TKYNMDvKazUMa3WwQ==")]
	[InlineData(2, false, 1, 0, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 1, 1, PacketType.Init1, false, "OgnCOC//82RGgM+o6rX6DA==", "Q0hY8qaYejnTDKqB8Y8CQw==")]
	[InlineData(2, false, 1, 1, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 1, 1, PacketType.Command, false, "mJx1RVbXoYnQpzBMpKI5eQ==", "9oAWGsFfG/q4EdJ5IoQeOQ==")]
	[InlineData(2, false, 1, 1, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 1, 1, PacketType.Voice, false, "3Ej/s2ysA1M471os+Iqe/w==", "+Cpq45iH27J7Q0i16F73Yg==")]
	[InlineData(2, false, 1, 1, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 1, 100, PacketType.Init1, false, "rxEdvWJC8EBMg1sA7r4hBg==", "4Dlr/7alaRBrC24YEh9Gzg==")]
	[InlineData(2, false, 1, 100, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 1, 100, PacketType.Command, false, "IcZnzSA2KpGMWLVk/MuHLw==", "rsWaLk37mje+yntPPPfhLA==")]
	[InlineData(2, false, 1, 100, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 1, 100, PacketType.Voice, false, "tpf0eYTzixmFLXXG4zZndg==", "JKKtnvzl0omFP1QCGuQbRw==")]
	[InlineData(2, false, 1, 100, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 100, 0, PacketType.Init1, false, "+j6QHVFrFa5DW4uPyrPRGQ==", "jRigPizU7CcMtPR+yEe2vQ==")]
	[InlineData(2, false, 100, 0, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 100, 0, PacketType.Command, false, "lJi2lalci8isGX7CxjStdA==", "KIwyOcMc9sTEmHXqqmOpzg==")]
	[InlineData(2, false, 100, 0, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 100, 0, PacketType.Voice, false, "c+GipY++i4VpttAWz7+v0g==", "VqGq1TKYNMDvKazUMa3WwQ==")]
	[InlineData(2, false, 100, 0, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 100, 1, PacketType.Init1, false, "OmzCOC//82RGgM+o6rX6DA==", "Q0hY8qaYejnTDKqB8Y8CQw==")]
	[InlineData(2, false, 100, 1, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 100, 1, PacketType.Command, false, "mPl1RVbXoYnQpzBMpKI5eQ==", "9oAWGsFfG/q4EdJ5IoQeOQ==")]
	[InlineData(2, false, 100, 1, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 100, 1, PacketType.Voice, false, "3C3/s2ysA1M471os+Iqe/w==", "+Cpq45iH27J7Q0i16F73Yg==")]
	[InlineData(2, false, 100, 1, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 100, 100, PacketType.Init1, false, "r3QdvWJC8EBMg1sA7r4hBg==", "4Dlr/7alaRBrC24YEh9Gzg==")]
	[InlineData(2, false, 100, 100, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 100, 100, PacketType.Command, false, "IaNnzSA2KpGMWLVk/MuHLw==", "rsWaLk37mje+yntPPPfhLA==")]
	[InlineData(2, false, 100, 100, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, false, 100, 100, PacketType.Voice, false, "tvL0eYTzixmFLXXG4zZndg==", "JKKtnvzl0omFP1QCGuQbRw==")]
	[InlineData(2, false, 100, 100, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 0, 0, PacketType.Init1, false, "9dRge/2qYUF4i+7ctfpxVg==", "3Ol0Ra4kJPcTd+r25ytOpA==")]
	[InlineData(2, true, 0, 0, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 0, 0, PacketType.Command, false, "4W7YbvYTOP4oIIQYrT8Lkg==", "ynwZLIU/OAmHn7WxvKkYTg==")]
	[InlineData(2, true, 0, 0, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 0, 0, PacketType.Voice, false, "Rmm8z65JciPhwcwghLG1zg==", "tNMjlgSBVx/PlB4p79HS1A==")]
	[InlineData(2, true, 0, 0, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 0, 1, PacketType.Init1, false, "68W4jXbE77ov8mePi/3TMg==", "PtSoyJxUcNQio5RjYgwHAg==")]
	[InlineData(2, true, 0, 1, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 0, 1, PacketType.Command, false, "c+Zs4+GUiHMOIrhZYg9j2A==", "Kze77bxuVPgrptnTnHXejg==")]
	[InlineData(2, true, 0, 1, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 0, 1, PacketType.Voice, false, "6IsN5U30iFDruPt/QspHGA==", "tVfqpJRPfrW8elcM88ke4w==")]
	[InlineData(2, true, 0, 1, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 0, 100, PacketType.Init1, false, "Gi4cAZvmpnRJsACLAwRIrw==", "+oEPrUtRiWbiKLtWXjgLKA==")]
	[InlineData(2, true, 0, 100, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 0, 100, PacketType.Command, false, "vrhp6eh+obVWudqZSQsQwQ==", "JWmOz7CNqa2NfHS2px3gng==")]
	[InlineData(2, true, 0, 100, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 0, 100, PacketType.Voice, false, "G6rhWiXZL+nV1uhHZAyZRg==", "fm51y1r9CSsWKPQTzzCFXQ==")]
	[InlineData(2, true, 0, 100, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 1, 0, PacketType.Init1, false, "9dVge/2qYUF4i+7ctfpxVg==", "3Ol0Ra4kJPcTd+r25ytOpA==")]
	[InlineData(2, true, 1, 0, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 1, 0, PacketType.Command, false, "4W/YbvYTOP4oIIQYrT8Lkg==", "ynwZLIU/OAmHn7WxvKkYTg==")]
	[InlineData(2, true, 1, 0, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 1, 0, PacketType.Voice, false, "Rmi8z65JciPhwcwghLG1zg==", "tNMjlgSBVx/PlB4p79HS1A==")]
	[InlineData(2, true, 1, 0, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 1, 1, PacketType.Init1, false, "68S4jXbE77ov8mePi/3TMg==", "PtSoyJxUcNQio5RjYgwHAg==")]
	[InlineData(2, true, 1, 1, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 1, 1, PacketType.Command, false, "c+ds4+GUiHMOIrhZYg9j2A==", "Kze77bxuVPgrptnTnHXejg==")]
	[InlineData(2, true, 1, 1, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 1, 1, PacketType.Voice, false, "6IoN5U30iFDruPt/QspHGA==", "tVfqpJRPfrW8elcM88ke4w==")]
	[InlineData(2, true, 1, 1, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 1, 100, PacketType.Init1, false, "Gi8cAZvmpnRJsACLAwRIrw==", "+oEPrUtRiWbiKLtWXjgLKA==")]
	[InlineData(2, true, 1, 100, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 1, 100, PacketType.Command, false, "vrlp6eh+obVWudqZSQsQwQ==", "JWmOz7CNqa2NfHS2px3gng==")]
	[InlineData(2, true, 1, 100, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 1, 100, PacketType.Voice, false, "G6vhWiXZL+nV1uhHZAyZRg==", "fm51y1r9CSsWKPQTzzCFXQ==")]
	[InlineData(2, true, 1, 100, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 100, 0, PacketType.Init1, false, "9bBge/2qYUF4i+7ctfpxVg==", "3Ol0Ra4kJPcTd+r25ytOpA==")]
	[InlineData(2, true, 100, 0, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 100, 0, PacketType.Command, false, "4QrYbvYTOP4oIIQYrT8Lkg==", "ynwZLIU/OAmHn7WxvKkYTg==")]
	[InlineData(2, true, 100, 0, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 100, 0, PacketType.Voice, false, "Rg28z65JciPhwcwghLG1zg==", "tNMjlgSBVx/PlB4p79HS1A==")]
	[InlineData(2, true, 100, 0, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 100, 1, PacketType.Init1, false, "66G4jXbE77ov8mePi/3TMg==", "PtSoyJxUcNQio5RjYgwHAg==")]
	[InlineData(2, true, 100, 1, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 100, 1, PacketType.Command, false, "c4Js4+GUiHMOIrhZYg9j2A==", "Kze77bxuVPgrptnTnHXejg==")]
	[InlineData(2, true, 100, 1, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 100, 1, PacketType.Voice, false, "6O8N5U30iFDruPt/QspHGA==", "tVfqpJRPfrW8elcM88ke4w==")]
	[InlineData(2, true, 100, 1, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 100, 100, PacketType.Init1, false, "GkocAZvmpnRJsACLAwRIrw==", "+oEPrUtRiWbiKLtWXjgLKA==")]
	[InlineData(2, true, 100, 100, PacketType.Init1, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 100, 100, PacketType.Command, false, "vtxp6eh+obVWudqZSQsQwQ==", "JWmOz7CNqa2NfHS2px3gng==")]
	[InlineData(2, true, 100, 100, PacketType.Command, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	[InlineData(2, true, 100, 100, PacketType.Voice, false, "G87hWiXZL+nV1uhHZAyZRg==", "fm51y1r9CSsWKPQTzzCFXQ==")]
	[InlineData(2, true, 100, 100, PacketType.Voice, true, "Yzpcd2luZG93c1xzeXN0ZQ==", "bVxmaXJld2FsbDMyLmNwbA==")]
	public void TestCryptoSecrets(
		int cryptVersion,
		bool fromServer, ushort packetId, uint generationId, PacketType packetType, bool dummyEncryption,
		string expectedKey, string expectedNonce)
	{
		// Arrange
		var tsCrypt = SetupCrypt(cryptVersion);

		// Act
		var (key, nonce) = tsCrypt.GetKeyNonce(fromServer, packetId, generationId, packetType, dummyEncryption);

		// Assert
		var expectedKeyBytes = Convert.FromBase64String(expectedKey);
		var expectedNonceBytes = Convert.FromBase64String(expectedNonce);

		Assert.Equal(expectedKeyBytes, key);
		Assert.Equal(expectedNonceBytes, nonce);
	}

	private static TsCrypt SetupCrypt(int version)
	{
		var tsCrypt = new TsCrypt(ClientIdentity);
		switch (version)
		{
		case CryptVariant1:
			tsCrypt.CryptoInit(TestV1Alpha, TestV1Beta, TestV1Omega).Unwrap();
			break;
		case CryptVariant2:
		{
			var privateKeyBytes = Convert.FromBase64String(TestV2PrivateKey);
			tsCrypt.CryptoInit2(TestV2Licence, Test2VOmega, TestV2Proof, TestV2Beta, privateKeyBytes).Unwrap();
			break;
		}
		default:
			throw new ArgumentException($"Unsupported crypt version {version}");
		}

		return tsCrypt;
	}

	// [Fact]
	// ReSharper disable once UnusedMember.Global
#pragma warning disable xUnit1013
	public void GenerateReferenceTestData()
#pragma warning restore xUnit1013
	{
		static bool[] BoolVariants() => [false, true];

		foreach (var protoVersion in new[] { CryptVariant1, CryptVariant2 })
		foreach (var fromServer in BoolVariants())
		foreach (var packetId in new ushort[] { 0, 1, 100 })
		foreach (var generationId in new uint[] { 0, 1, 100 })
		foreach (var packetType in new[] { PacketType.Init1, PacketType.Command, PacketType.Voice })
		foreach (var dummyEncryption in BoolVariants())
		{
			var tsCrypt = SetupCrypt(protoVersion);

			var (key, nonce) = tsCrypt.GetKeyNonce(fromServer, packetId, generationId, packetType, dummyEncryption);
			var actualBase64Key = Convert.ToBase64String(key);
			var actualBase64Nonce = Convert.ToBase64String(nonce);

			static string BoolS(bool b) => b ? "true" : "false";
			var line = new StringBuilder()
				.Append("[InlineData(")
				.Append($"{protoVersion}, ")
				.Append($"{BoolS(fromServer)}, ")
				.Append($"{packetId}, ")
				.Append($"{generationId}, ")
				.Append($"{nameof(PacketType)}.{packetType}, ")
				.Append($"{BoolS(dummyEncryption)}, ")
				.Append($"\"{actualBase64Key}\", ")
				.Append($"\"{actualBase64Nonce}\"")
				.Append(")]");

			Console.WriteLine(line);
		}
	}
}
