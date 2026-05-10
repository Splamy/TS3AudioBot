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

namespace TS3AudioBot.CommandSystem.Text;

public partial class TextModBuilder
{
	[GeneratedRegex(@"{\d+}", RegexOptions.ExplicitCapture)]
	private static partial Regex Placeholder { get; }

	private readonly bool color;
	private readonly StringBuilder strb;
	private TextMod cur = TextMod.None;

	public int Length { get => strb.Length; set => strb.Length = value; }

	public TextModBuilder(bool color = true)
		: this(new StringBuilder(), color) { }

	public TextModBuilder(StringBuilder strb, bool color = true)
	{
		ArgumentNullException.ThrowIfNull(strb);
		this.strb = strb;
		this.color = color;
	}

	public TextModBuilder Append(AppliedTextMod atm) => Append(atm.Text, atm.Mod);

	public TextModBuilder Append(ReadOnlySpan<char> text, TextMod mod)
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

	public TextModBuilder AppendLine(ReadOnlySpan<char> text, TextMod mod)
	{
		Append(text, mod);
		strb.Append('\n');
		return this;
	}

	public TextModBuilder AppendFormat(AppliedTextMod format, params ReadOnlySpan<AppliedTextMod> para)
	{
		if (format.Text is null) throw new ArgumentNullException(nameof(format));
		if (para.Length == 0)
		{
			Append(format);
		}
		else
		{
			var textSpan = format.Text.AsSpan();
			Range? lastSpan = null;
			foreach (var split in Placeholder.EnumerateSplits(textSpan))
			{
				if (lastSpan is { } lastInner)
				{
					Append(textSpan[lastInner], format.Mod);
					var fill = textSpan[lastInner.End..split.Start];
					var fillNum = int.Parse(fill[1..^1]);
					Append(para[fillNum]);
				}
				lastSpan = split;
			}

			if (lastSpan is { } last)
			{
				Append(textSpan[last], format.Mod);
			}
		}

		return this;
	}

	private static void StartText(StringBuilder strb, ReadOnlySpan<char> text, ref TextMod cur, TextMod mod)
	{
		if (text.IsEmpty)
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
			mod.HasColor.GetValueOrDefault().GetL(strb);
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
			strb.Append("[/COLOR]");
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
