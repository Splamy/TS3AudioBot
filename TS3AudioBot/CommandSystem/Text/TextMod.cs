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
	using System.Collections.Generic;
	using System.Text.RegularExpressions;

	public readonly struct TextMod
	{
		public static readonly TextMod None = new TextMod(0, null);

		public TextModFlag Flags { get; }
		public Color? HasColor { get; }

		public TextMod(TextModFlag flags, Color? color = null)
		{
			this.Flags = flags;
			HasColor = color;
		}

		public TextMod Color(Color color) => new TextMod(Flags | TextModFlag.Color, color);
		public TextMod Bold() => new TextMod(Flags | TextModFlag.Bold, HasColor);
		public TextMod Italic() => new TextMod(Flags | TextModFlag.Italic, HasColor);
		public TextMod Strike() => new TextMod(Flags | TextModFlag.Strike, HasColor);
		public TextMod Underline() => new TextMod(Flags | TextModFlag.Underline, HasColor);

		public static string Format(AppliedTextMod format, params AppliedTextMod[] para)
		{
			var mods = new Stack<TextModFlag>();
			var tmb = new TextModBuilder();

			if (para.Length == 0)
			{
				tmb.Append(format);
			}
			else
			{
				var parts = Regex.Split(format.Text, @"{\d+}");

				for (int i = 0; i < parts.Length - 1; i++)
				{
					tmb.Append(parts[i], format.Mod);
					tmb.Append(para[i]);
				}
				tmb.Append(parts[parts.Length - 1], format.Mod);
			}

			return tmb.ToString();
		}

		public static string Format(bool color, AppliedTextMod format, params AppliedTextMod[] para)
		{
			return color ? Format(format, para) : string.Format(format.Text, para);
		}
	}
}
