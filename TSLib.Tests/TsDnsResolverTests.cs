using System.Net;

namespace TSLib.Tests;

public class TsDnsResolverTests
{
	[Theory]
	[InlineData("127.0.0.1", 9987, "127.0.0.1", 9987)]
	[InlineData("127.0.0.1:0", 9987, "127.0.0.1", 9987)]
	[InlineData("127.0.0.1:1234", 9987, "127.0.0.1", 1234)]
	[InlineData("::1", 9987, "::1", 9987)]
	[InlineData("[::1]", 9987, "::1", 9987)]
	[InlineData("[::1]:0", 9987, "::1", 9987)]
	[InlineData("[::1]:1234", 9987, "::1", 1234)]
	[InlineData("localhost", 9987, "127.0.0.1", 9987)]
	[InlineData("localhost:1234", 9987, "127.0.0.1", 1234)]
	[InlineData("127.0.0.1", 10011, "127.0.0.1", 10011)]
	public async Task ResolveIpAddressUsesExpectedPort(string address, ushort defaultPort, string expectedAddress,
		ushort expectedPort)
	{
		var endPoint = await TsDnsResolver.TryResolveUncached(address, defaultPort);

		Assert.NotNull(endPoint);
		Assert.Equal(IPAddress.Parse(expectedAddress), endPoint.Address);
		Assert.Equal(expectedPort, endPoint.Port);
	}
}
