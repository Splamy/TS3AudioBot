// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TS3AudioBot.CommandSystem.Text
{
	public readonly struct TextMod : IEquatable<TextMod>
	{
		public static readonly TextMod None = new TextMod(0, null);

		public TextModFlag Flags { get; }
		public Color? HasColor { get; }

		public TextMod(TextModFlag flags, Color? color = null)
		{
			Flags = flags;
			HasColor = color;
		}

		public readonly TextMod Color(Color color) => new TextMod(Flags | TextModFlag.Color, color);
		public readonly TextMod Bold() => new TextMod(Flags | TextModFlag.Bold, HasColor);
		public readonly TextMod Italic() => new TextMod(Flags | TextModFlag.Italic, HasColor);
		public readonly TextMod Strike() => new TextMod(Flags | TextModFlag.Strike, HasColor);
		public readonly TextMod Underline() => new TextMod(Flags | TextModFlag.Underline, HasColor);

		public static string Format(AppliedTextMod format, params AppliedTextMod[] para)
			=> new TextModBuilder().AppendFormat(format, para).ToString();

		public static string Format(bool color, AppliedTextMod format, params AppliedTextMod[] para)
		{
			if (color)
				return Format(format, para);
			if (string.IsNullOrEmpty(format.Text))
				return string.Empty;
			return string.Format(format.Text, para);
		}

		public readonly bool Equals(TextMod other) => Flags == other.Flags && HasColor == other.HasColor;
		public override readonly bool Equals(object? obj) => obj is TextMod tm && Equals(tm);
		public static bool operator ==(TextMod a, TextMod b) => a.Flags == b.Flags && a.HasColor == b.HasColor;
		public static bool operator !=(TextMod a, TextMod b) => a.Flags != b.Flags || a.HasColor != b.HasColor;
		public override readonly int GetHashCode() => ((int)Flags << 28) | HasColor.GetHashCode();
	}
}
