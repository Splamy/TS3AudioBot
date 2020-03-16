// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;

namespace TS3AudioBot.History
{
	public interface IHistoryFormatter
	{
		string ProcessQuery(AudioLogEntry entry, Func<AudioLogEntry, string> format);
		string ProcessQuery(IEnumerable<AudioLogEntry> entries, Func<AudioLogEntry, string> format);
	}

	// needed ?
	public enum HistoryDisplayColumn
	{
		AleId,
		UserDbId,
		UserName,
		AleTitle,
	}
}
