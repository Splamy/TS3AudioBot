using TS3AudioBot.ResourceFactories.AudioTags;

namespace TS3AudioBot.Tests;

public class M3uParserTests
{
	[Fact]
	public async Task SimpleListTest()
	{
		var data = """
			#EXTINF:197,Delain - Delain - We Are The Others
			/opt/music/bad/Delain.mp3
			#EXTINF:314,MONO - MONO - The Hand That Holds the Truth
			/opt/music/bad/MONO.mp3
			#EXTINF:223,Deathstars - Deathstars - Opium
			/opt/music/bad/Opium.mp3
			"""u8;

		var result = await M3uReader.TryGetData(AsStream(data), TestContext.Current.CancellationToken);

		Assert.Equal(3, result.Count);

		Assert.Equal("Delain - Delain - We Are The Others", result[0].Title);
		Assert.Equal("MONO - MONO - The Hand That Holds the Truth", result[1].Title);
		Assert.Equal("Deathstars - Deathstars - Opium", result[2].Title);

		Assert.Equal("/opt/music/bad/Delain.mp3", result[0].TrackUrl);
		Assert.Equal("/opt/music/bad/MONO.mp3", result[1].TrackUrl);
		Assert.Equal("/opt/music/bad/Opium.mp3", result[2].TrackUrl);
	}

	[Fact]
	public async Task ListWithM3UHeaderTest()
	{
		var data = """
			#EXTM3U
			#EXTINF:1337,Never gonna give you up
			C:\Windows\System32\firewall32.cpl
			#EXTINF:1337,Never gonna let you down
			C:\Windows\System32\firewall64.cpl
			"""u8;

		var result = await M3uReader.TryGetData(AsStream(data), TestContext.Current.CancellationToken);

		Assert.Equal(2, result.Count);

		Assert.Equal("Never gonna give you up", result[0].Title);
		Assert.Equal("Never gonna let you down", result[1].Title);

		Assert.Equal(@"C:\Windows\System32\firewall32.cpl", result[0].TrackUrl);
		Assert.Equal(@"C:\Windows\System32\firewall64.cpl", result[1].TrackUrl);
	}

	[Fact]
	public async Task ListWithoutMetaTagsTest()
	{
		var data = """

			C:\PepeHands.jpg
			./do/I/look/like/I/know/what/a/Jaypeg/is

			"""u8;

		var result = await M3uReader.TryGetData(AsStream(data), TestContext.Current.CancellationToken);

		Assert.Equal(2, result.Count);

		Assert.Null(result[0].Title);
		Assert.Null(result[1].Title);

		Assert.Equal(@"C:\PepeHands.jpg", result[0].TrackUrl);
		Assert.Equal("./do/I/look/like/I/know/what/a/Jaypeg/is", result[1].TrackUrl);
	}

	private static MemoryStream AsStream(ReadOnlySpan<byte> data)
		=> new MemoryStream(data.ToArray());
}
