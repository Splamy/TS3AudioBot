namespace TS3AudioBot.CommandSystem
{
	using System;
	using TS3Query.Messages;

	public class ExecutionInformation : MarshalByRefObject
	{
		public BotSession Session { get; }
		public TextMessage TextMessage { get; }
		public Lazy<bool> IsAdmin { get; }

		private ExecutionInformation() { Session = null; TextMessage = null; IsAdmin = new Lazy<bool>(() => true); }
		public ExecutionInformation(BotSession session, TextMessage textMessage, Lazy<bool> isAdmin)
		{
			Session = session;
			TextMessage = textMessage;
			IsAdmin = isAdmin;
		}

		public static readonly ExecutionInformation Debug = new ExecutionInformation();
	}
}
