using NUnit.Framework;
using System.IO;
using System.Text;
using TS3AudioBot.ResourceFactories.AudioTags;

namespace TS3ABotUnitTests
{
	[TestFixture]
	internal class M3uParserTests
	{
		[Test]
		public void SimpleListTest()
		{
			var result = M3uReader.TryGetData(new MemoryStream(Encoding.UTF8.GetBytes(
@"#EXTINF:197,Delain - Delain - We Are The Others
/opt/music/bad/Delain.mp3
#EXTINF:314,MONO - MONO - The Hand That Holds the Truth
/opt/music/bad/MONO.mp3
#EXTINF:223,Deathstars - Deathstars - Opium
/opt/music/bad/Opium.mp3"
				)));
			Assert.That(result.Ok);
			Assert.AreEqual(3, result.Value.Count);

			Assert.AreEqual("Delain - Delain - We Are The Others", result.Value[0].Title);
			Assert.AreEqual("MONO - MONO - The Hand That Holds the Truth", result.Value[1].Title);
			Assert.AreEqual("Deathstars - Deathstars - Opium", result.Value[2].Title);

			Assert.AreEqual("/opt/music/bad/Delain.mp3", result.Value[0].TrackUrl);
			Assert.AreEqual("/opt/music/bad/MONO.mp3", result.Value[1].TrackUrl);
			Assert.AreEqual("/opt/music/bad/Opium.mp3", result.Value[2].TrackUrl);
		}

		[Test]
		public void ListWithM3uHeaderTest()
		{
			var result = M3uReader.TryGetData(new MemoryStream(Encoding.UTF8.GetBytes(
@"#EXTM3U
#EXTINF:1337,Never gonna give you up
C:\Windows\System32\firewall32.cpl
#EXTINF:1337,Never gonna let you down
C:\Windows\System32\firewall64.cpl"
				)));
			Assert.That(result.Ok);
			Assert.AreEqual(2, result.Value.Count);

			Assert.AreEqual("Never gonna give you up", result.Value[0].Title);
			Assert.AreEqual("Never gonna let you down", result.Value[1].Title);

			Assert.AreEqual(@"C:\Windows\System32\firewall32.cpl", result.Value[0].TrackUrl);
			Assert.AreEqual(@"C:\Windows\System32\firewall64.cpl", result.Value[1].TrackUrl);
		}


		[Test]
		public void ListWithoutMetaTagsTest()
		{
			var result = M3uReader.TryGetData(new MemoryStream(Encoding.UTF8.GetBytes(
@"
C:\PepeHands.jpg
./do/I/look/like/I/know/what/a/Jaypeg/is
"
				)));
			Assert.That(result.Ok);
			Assert.AreEqual(2, result.Value.Count);

			Assert.AreEqual(null, result.Value[0].Title);
			Assert.AreEqual(null, result.Value[1].Title);

			Assert.AreEqual(@"C:\PepeHands.jpg", result.Value[0].TrackUrl);
			Assert.AreEqual(@"./do/I/look/like/I/know/what/a/Jaypeg/is", result.Value[1].TrackUrl);
		}
	}
}
