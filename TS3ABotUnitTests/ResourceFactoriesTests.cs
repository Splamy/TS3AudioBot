using NUnit.Framework;
using TS3AudioBot.Config;
using TS3AudioBot.ResourceFactories;
using TS3AudioBot.ResourceFactories.Youtube;

namespace TS3ABotUnitTests
{
	[TestFixture]
	public class ResourceFactoriesTests
	{
		[Test]
		public void Factory_YoutubeFactoryTest()
		{
			var ctx = new ResolveContext(null, null);
			using IResourceResolver rfac = new YoutubeResolver(new ConfResolverYoutube());
			// matching links
			Assert.AreEqual(rfac.MatchResource(ctx, "https://www.youtube.com/watch?v=robqdGEhQWo"), MatchCertainty.Always);
			Assert.AreEqual(rfac.MatchResource(ctx, "https://youtu.be/robqdGEhQWo"), MatchCertainty.Always);
			Assert.AreEqual(rfac.MatchResource(ctx, "https://discarded-ideas.org/sites/discarded-ideas.org/files/music/darkforestkeep_symphonic.mp3"), MatchCertainty.Never);
			Assert.AreNotEqual(rfac.MatchResource(ctx, "http://splamy.de/youtube.com/youtu.be/fake.mp3"), MatchCertainty.Always);

			// restoring links
			Assert.AreEqual("https://youtu.be/robqdGEhQWo", rfac.RestoreLink(ctx, new AudioResource { ResourceId = "robqdGEhQWo" }));
		}
	}
}
