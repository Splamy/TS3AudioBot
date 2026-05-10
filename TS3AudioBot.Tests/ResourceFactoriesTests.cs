using TS3AudioBot.Config;
using TS3AudioBot.ResourceFactories;
using TS3AudioBot.ResourceFactories.Youtube;

namespace TS3AudioBot.Tests;

public class ResourceFactoriesTests
{
	[Fact]
	public void Factory_YoutubeFactoryTest()
	{
		var ctx = new ResolveContext(null, null);
		using IResourceResolver rfac = new YoutubeResolver(new ConfResolverYoutube());
		// matching links
		Assert.Equal(MatchCertainty.Always, rfac.MatchResource(ctx, "https://www.youtube.com/watch?v=robqdGEhQWo"));
		Assert.Equal(MatchCertainty.Always, rfac.MatchResource(ctx, "https://youtu.be/robqdGEhQWo"));
		Assert.Equal(MatchCertainty.Always, rfac.MatchResource(ctx, "https://www.youtube.com/shorts/OOXZx0QkY04"));
		Assert.Equal(MatchCertainty.Never, rfac.MatchResource(ctx, "https://discarded-ideas.org/sites/discarded-ideas.org/files/music/darkforestkeep_symphonic.mp3"));
		Assert.NotEqual(MatchCertainty.Always, rfac.MatchResource(ctx, "http://splamy.de/youtube.com/youtu.be/fake.mp3"));

		// restoring links
		Assert.Equal("https://youtu.be/robqdGEhQWo", rfac.RestoreLink(ctx, new AudioResource { ResourceId = "robqdGEhQWo" }));
	}
}
