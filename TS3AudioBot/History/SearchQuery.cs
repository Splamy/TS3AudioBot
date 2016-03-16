namespace TS3AudioBot.History
{
	using System;

	public class SeachQuery
	{
		public string TitlePart;
		public uint? UserId;
		public DateTime? LastInvokedAfter;
		public int MaxResults;

		public SeachQuery()
		{
			TitlePart = null;
			UserId = null;
			LastInvokedAfter = null;
			MaxResults = 10;
		}
	}
}
