// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace TS3AudioBot.CommandSystem.Text
{
	public class TextModBuilder
	{
		private readonly bool color;
		private readonly StringBuilder strb;
		private TextMod cur = TextMod.None;

		public int Length { get => strb.Length; set => strb.Length = value; }

		public TextModBuilder(bool color = true)
			: this(new StringBuilder(), color) { }

		public TextModBuilder(StringBuilder strb, bool color = true)
		{
			this.strb = strb ?? throw new ArgumentNullException(nameof(strb));
			this.color = color;
		}

		public TextModBuilder Append(AppliedTextMod atm) => Append(atm.Text, atm.Mod);

		public TextModBuilder Append(string text, TextMod mod)
		{
			if (color)
				StartText(strb, text, ref cur, mod);
			else
				strb.Append(text);
			return this;
		}

		public TextModBuilder AppendLine(AppliedTextMod atm)
		{
			Append(atm.Text, atm.Mod);
			strb.Append('\n');
			return this;
		}

		public TextModBuilder AppendLine(string text, TextMod mod)
		{
			Append(text, mod);
			strb.Append('\n');
			return this;
		}

		public TextModBuilder AppendFormat(AppliedTextMod format, params AppliedTextMod[] para)
		{
			if (para.Length == 0)
			{
				Append(format);
			}
			else
			{
				var parts = Regex.Split(format.Text, @"{\d+}");

				for (int i = 0; i < parts.Length - 1; i++)
				{
					Append(parts[i], format.Mod);
					Append(para[i]);
				}
				Append(parts[parts.Length - 1], format.Mod);
			}

			return this;
		}

		private static void StartText(StringBuilder strb, string text, ref TextMod cur, TextMod mod)
		{
			if (string.IsNullOrEmpty(text))
				return;
			var curFlags = cur.Flags;
			var modFlags = mod.Flags;
			var close = curFlags & ~modFlags;
			if ((curFlags & modFlags).HasFlag(TextModFlag.Color) && cur.HasColor != mod.HasColor) close |= TextModFlag.Color;
			var trimClose = GetShortest(close);
			curFlags = End(strb, curFlags, trimClose);
			curFlags &= (~(trimClose - 1) | modFlags);
			curFlags = Start(strb, curFlags, mod);
			cur = new TextMod(curFlags, mod.HasColor);
			strb.Append(text);
		}

		private static TextModFlag Start(StringBuilder strb, TextModFlag cur, TextMod mod)
		{
			var flag = ~cur & mod.Flags;
			if (flag.HasFlag(TextModFlag.Bold))
				strb.Append("[B]");
			if (flag.HasFlag(TextModFlag.Italic))
				strb.Append("[I]");
			if (flag.HasFlag(TextModFlag.Strike))
				strb.Append("[S]");
			if (flag.HasFlag(TextModFlag.Underline))
				strb.Append("[U]");
			if (flag.HasFlag(TextModFlag.Color))
				mod.HasColor.Value.GetL(strb);
			return cur | mod.Flags;
		}

		private static TextModFlag End(StringBuilder strb, TextModFlag cur, TextModFlag mod)
		{
			var flag = mod;
			if (flag.HasFlag(TextModFlag.Bold))
				strb.Append("[/B]");
			if (flag.HasFlag(TextModFlag.Italic))
				strb.Append("[/I]");
			if (flag.HasFlag(TextModFlag.Strike))
				strb.Append("[/S]");
			if (flag.HasFlag(TextModFlag.Underline))
				strb.Append("[/U]");
			if (flag.HasFlag(TextModFlag.Color))
				strb.Append($"[/COLOR]");
			return cur & ~mod;
		}

		private static TextModFlag GetShortest(TextModFlag mod)
		{
			if (mod.HasFlag(TextModFlag.Bold)) return TextModFlag.Bold;
			if (mod.HasFlag(TextModFlag.Italic)) return TextModFlag.Italic;
			if (mod.HasFlag(TextModFlag.Strike)) return TextModFlag.Strike;
			if (mod.HasFlag(TextModFlag.Underline)) return TextModFlag.Underline;
			if (mod.HasFlag(TextModFlag.Color)) return TextModFlag.Color;
			return 0;
		}

		public override string ToString() => strb.ToString();
	}
}
