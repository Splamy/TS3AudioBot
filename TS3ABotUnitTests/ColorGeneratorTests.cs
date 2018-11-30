using NUnit.Framework;
using TS3AudioBot.CommandSystem.Text;

namespace TS3ABotUnitTests
{
	[TestFixture]
	public class ColorGeneratorTests
	{
		[Test]
		public void Color1Test()
		{
			var res = TextMod.Format("Hello {0}".Mod().Color(Color.Red).Bold(), "World".Mod().Bold());
			Assert.AreEqual("[B][COLOR=#FF0000]Hello [/COLOR]World", res);
		}

		[Test]
		public void Color2Test()
		{
			var res = TextMod.Format("Hello {0}".Mod().Color(Color.Red).Bold(), "World".Mod().Bold().Italic());
			Assert.AreEqual("[B][COLOR=#FF0000]Hello [/COLOR][I]World", res);
		}

		[Test]
		public void Color3Test()
		{
			var res = TextMod.Format("Hello {0}{1}".Mod().Color(Color.Red).Bold(),
				"World".Mod().Bold().Italic(),
				", How are you?".Mod().Underline());
			Assert.AreEqual("[B][COLOR=#FF0000]Hello [/COLOR][I]World[/B][U], How are you?", res);
		}

		[Test]
		public void Color4Test()
		{
			var res = TextMod.Format("Hello {0} but {1}".Mod().Color(Color.Red).Bold(),
				   "World".Mod().Bold().Italic(),
				   ", How are you?".Mod().Underline());
			Assert.AreEqual("[B][COLOR=#FF0000]Hello [/COLOR][I]World[/I][COLOR=#FF0000] but [/B][U], How are you?", res);
		}

		[Test]
		public void Color5Test()
		{
			var res = TextMod.Format("Hello {0} but {1}".Mod().Color(Color.Red).Bold(),
					"World".Mod().Bold().Italic().Strike(),
					", How are you?".Mod().Underline());
			Assert.AreEqual("[B][COLOR=#FF0000]Hello [/COLOR][I][S]World[/I][COLOR=#FF0000] but [/B][U], How are you?", res);
		}
	}
}
