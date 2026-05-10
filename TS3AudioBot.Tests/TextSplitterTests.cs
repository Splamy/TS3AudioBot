using System.Collections.Immutable;
using System.Linq;
using System.Text;
using TS3AudioBot.CommandSystem.Text;
using TSLib.Commands;

namespace TS3AudioBot.Tests;

public class TextSplitterTests
{
	private const int MaxSplit = 8192;

	public static TheoryData<string> Data =>
	[
		// Mixed characters
		"""
		Playlist: "[B]asdf2[/B]" with 110 songs.
		0: 【nami】 One of Repetition -「繰り返し一粒 」を歌ってみた
		1: God knows... ''The Melancholy of Haruhi Suzumiya'' 【涼宮ハルヒの憂鬱】Kadokawa公認MAD【ﾍﾞｰｽ 演奏】
		2: Noucome op Full
		3: 麻枝 准×やなぎなぎ「無敵のSoldier」
		4: Nisemonogatari Opening 3 - Platinum Disco (Yuka Iguchi) HD
		5: SAO II OP   Courage Full
		6: SAO II OP Ignite Full
		7: 「Secret base～君がくれたもの～」cover by【Mesｘmokonaｘ冥月ｘ洛】
		8: [HQ] Yousei Teikoku - Kokou no Sousei
		9: Yousei Teikoku - Kikai Shoujo Gensou
		10: Yousei Teikoku-  Tasogare no Gekka
		11: Yousei Teikoku - Wahrheit
		12: 【Karaoke】IA IA ★ Night of Desire【on vocal】 samfree
		13: [1080P Full風] Luka Luka★Night Fever ルカルカ★ナイトフィーバー 巡音ルカ Project DIVA English lyrics romaji subtitles
		14: Vocaloid - Nekomura Iroha - Cat Cat ☆Super Fever Night
		15: [Piko] "Piko Piko ☆Legend Of The Night " english subbed [english / romaji in the description]
		16: 【MMD】 Pomp And Circumstance 【Yukari & Lily】
		17: 【MMD】 Two Faced Lovers (Nikoman Ver.) 【CUL】
		18: 【CUL】「Aokigahara -青木ヶ原-」【Vocaloidカバー】
		19: 【MMD】 LUVORATORRRRRY! 【Kagamine Rin & GUMI】

		""",
		// Normal ASCII
		new string('a', 1024),
		// Special TS char
		new string('|', 1024),
		// '⮞' is a 3-byte long character encoded in UTF-8 ([]{ 226, 174, 158 })
		new string('⮞', 1024),
		// '😈' is a 4-byte long character encoded in UTF-8 ([]{ 240, 159, 152, 136 })
		new StringBuilder().Insert(0, "😈", 1024).ToString()
	];

	[Theory, MemberData(nameof(Data))]
	public void Split(string testMsg)
	{
		for (int i = 4; i < MaxSplit; i++)
		{
			var parts = LongTextTransform.Split(testMsg, LongTextBehaviour.SplitHard, maxMessageSize: i).ToArray();
			foreach (var part in parts)
			{
				Assert.True(TsString.TokenLength(part) <= i,
					$"Part length {TsString.TokenLength(part)} exceeds max message size {i}");
			}

			var joined = string.Concat(parts);
			Assert.Equal(testMsg, joined);
		}
	}
}
