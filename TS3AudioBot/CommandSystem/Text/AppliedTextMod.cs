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
	public class AppliedTextMod
	{
		public string Text { get; }
		public TextMod Mod { get; set; } = TextMod.None;

		public AppliedTextMod(string text)
		{
			Text = text;
		}

		public AppliedTextMod Color(Color color) { Mod = Mod.Color(color); return this; }
		public AppliedTextMod Bold() { Mod = Mod.Bold(); return this; }
		public AppliedTextMod Italic() { Mod = Mod.Italic(); return this; }
		public AppliedTextMod Underline() { Mod = Mod.Underline(); return this; }
		public AppliedTextMod Strike() { Mod = Mod.Strike(); return this; }

		public static implicit operator AppliedTextMod(string text) => new AppliedTextMod(text);

		public override string ToString() => Text;
	}
}
