// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem.Text;

public readonly record struct AppliedTextMod(string? Text, TextMod Mod)
{
	public AppliedTextMod(string? text) : this(text, TextMod.None) { }

	public AppliedTextMod Color(Color color) => new(Text, Mod.Color(color));
	public AppliedTextMod Bold() => new(Text, Mod.Bold());
	public AppliedTextMod Italic() => new(Text, Mod.Italic());
	public AppliedTextMod Underline() => new(Text, Mod.Underline());
	public AppliedTextMod Strike() => new(Text, Mod.Strike());

	public static implicit operator AppliedTextMod(string? text) => new(text);

	public override string? ToString() => Text;
}
