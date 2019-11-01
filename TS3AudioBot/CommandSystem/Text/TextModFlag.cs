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
	[Flags]
	public enum TextModFlag
	{
		None = 0,
		Color = 1 << 0,
		Underline = 1 << 1,
		Strike = 1 << 2,
		Italic = 1 << 3,
		Bold = 1 << 4,
	}
}
