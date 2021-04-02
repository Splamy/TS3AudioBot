// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem.Text
{
	public readonly struct AppliedTextMod
	{
		public string? Text { get; }
		public TextMod Mod { get; }

		public AppliedTextMod(string? text)
		{
			Text = text;
			Mod = TextMod.None;
		}

		public AppliedTextMod(string? text, TextMod mod)
		{
			Text = text;
			Mod = mod;
		}

		public readonly AppliedTextMod Color(Color color) => new AppliedTextMod(Text, Mod.Color(color));
		public readonly AppliedTextMod Bold() => new AppliedTextMod(Text, Mod.Bold());
		public readonly AppliedTextMod Italic() => new AppliedTextMod(Text, Mod.Italic());
		public readonly AppliedTextMod Underline() => new AppliedTextMod(Text, Mod.Underline());
		public readonly AppliedTextMod Strike() => new AppliedTextMod(Text, Mod.Strike());

		public static implicit operator AppliedTextMod(string? text) => new AppliedTextMod(text);

		public override readonly string? ToString() => Text;
	}
}
