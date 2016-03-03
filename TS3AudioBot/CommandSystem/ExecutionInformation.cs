namespace TS3AudioBot.CommandSystem
{
	using System;

	public class ExecutionInformation
	{
		public BotSession Session { get; set; }
		public TS3Query.Messages.TextMessage TextMessage { get; set; }
		public Lazy<bool> IsAdmin { get; set; }
	}
}
