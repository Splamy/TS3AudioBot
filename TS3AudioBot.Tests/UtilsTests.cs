using System.Text.RegularExpressions;
using TS3AudioBot.Helper;
using TSLib.Full;

namespace TS3AudioBot.Tests;

public class UtilsTests
{
	[Fact]
	public void UtilSeedTest()
	{
		var lowCaseRegex = new Regex("^[a-z]*$", Util.DefaultRegexConfig & ~RegexOptions.IgnoreCase);
		for (int i = 0; i < 100000; i++)
		{
			var str = Util.FromSeed(i);
			Assert.True(lowCaseRegex.IsMatch(str), "For seed: " + i);
			var roundtrip = Util.ToSeed(str);
			Assert.Equal(i, roundtrip);
		}
	}

	/* ======================= TSLib Tests ========================*/

	[Fact]
	public void VersionSelfCheck()
	{
		TsCrypt.VersionSelfCheck();
	}
}
