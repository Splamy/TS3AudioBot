namespace TS3AudioBot.History
{
	using System;
	using System.Collections.Generic;

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