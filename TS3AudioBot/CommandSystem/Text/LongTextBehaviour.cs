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
	public enum LongTextBehaviour
	{
		/// <summary>Splits the message preferably at line breaks.</summary>
		Split,
		/// <summary>Splits at exact maxium length per message.</summary>
		SplitHard,
		/// <summary>Discards the message.</summary>
		Drop,
	}
}
