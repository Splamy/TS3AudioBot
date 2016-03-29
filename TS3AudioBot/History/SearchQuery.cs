namespace TS3AudioBot.History
{
	using System;

	public class SeachQuery : MarshalByRefObject
	{
		public string TitlePart { get; set; }
		public uint? UserId { get; set; }
		public DateTime? LastInvokedAfter { get; set; }
		public int MaxResults { get; set; }

		public SeachQuery()
		{
			TitlePart = null;
			UserId = null;
			LastInvokedAfter = null;
			MaxResults = 10;
		}
	}
}
