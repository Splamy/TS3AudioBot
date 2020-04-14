using NUnit.Framework;
using System.Linq;
using TS3AudioBot.CommandSystem.Text;
using TSLib.Commands;

namespace TS3ABotUnitTests
{
	[TestFixture]
	public class TextSplitterTests
	{
		public const string Str1 = "Playlist: \"[B]asdf2[/B]\" with 110 songs.\n0: 【nami】 One of Repetition -「繰り返し一粒 」を歌ってみた\n1: God knows... ''The Melancholy of Haruhi Suzumiya'' 【涼宮ハルヒの憂鬱】Kadokawa公認MAD【ﾍﾞｰｽ 演奏】\n2: Noucome op Full\n3: 麻枝 准×やなぎなぎ「無敵のSoldier」\n4: Nisemonogatari Opening 3 - Platinum Disco (Yuka Iguchi) HD\n5: SAO II OP   Courage Full\n6: SAO II OP Ignite Full\n7: 「Secret base～君がくれたもの～」cover by【Mesｘmokonaｘ冥月ｘ洛】\n8: [HQ] Yousei Teikoku - Kokou no Sousei\n9: Yousei Teikoku - Kikai Shoujo Gensou\n10: Yousei Teikoku-  Tasogare no Gekka\n11: Yousei Teikoku - Wahrheit\n12: 【Karaoke】IA IA ★ Night of Desire【on vocal】 samfree\n13: [1080P Full風] Luka Luka★Night Fever ルカルカ★ナイトフィーバー 巡音ルカ Project DIVA English lyrics romaji subtitles\n14: Vocaloid - Nekomura Iroha - Cat Cat ☆Super Fever Night\n15: [Piko] \"Piko Piko ☆Legend Of The Night \" english subbed [english / romaji in the description]\n16: 【MMD】 Pomp And Circumstance 【Yukari & Lily】\n17: 【MMD】 Two Faced Lovers (Nikoman Ver.) 【CUL】\n18: 【CUL】「Aokigahara -青木ヶ原-」【Vocaloidカバー】\n19: 【MMD】 LUVORATORRRRRY! 【Kagamine Rin & GUMI】\n";
		public const int MaxSplit = 8192;

		[Test]
		public void Split()
		{
			for (int i = 4; i < MaxSplit; i++)
			{
				var parts = LongTextTransform.Split(Str1, LongTextBehaviour.SplitHard, maxMessageSize: i).ToArray();
				foreach (var part in parts)
				{
					Assert.LessOrEqual(TsString.TokenLength(part), i);
				}
				var joined = string.Concat(parts);
				Assert.AreEqual(Str1, joined);
			}
		}
	}
}
