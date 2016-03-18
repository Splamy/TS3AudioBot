namespace TS3ABotUnitTests
{
	using System;
	using System.Reflection;
	using NUnit.Framework;

	using TS3Query.Messages;

	using TS3AudioBot;
	using TS3AudioBot.CommandSystem;
	using TS3Query;

	[TestFixture]
	public class BotCommandTests
	{
		MainBot bot;

		public BotCommandTests()
		{
			bot = new MainBot();
			typeof(MainBot).GetProperty(nameof(MainBot.CommandManager)).SetValue(bot, new CommandManager());
			bot.CommandManager.RegisterMain(bot);
		}

		TextMessage CreateTextMessage()
		{
			var msg = Generator.ActivateNotification<TextMessage>();
			msg.InvokerId = 0;
			msg.InvokerName = "Invoker";
			msg.InvokerUid = "InvokerUid";
			msg.Message = "";
			msg.Target = MessageTarget.Private;
			return msg;
		}

		string CallCommand(string command)
		{
			var info = new ExecutionInformation
			{
				Session = null,
				TextMessage = CreateTextMessage(),
				IsAdmin = new Lazy<bool>(true)
			};
			return bot.CommandManager.CommandSystem.ExecuteCommand(info, command);
		}

		[Test]
		public void BotCommandTest()
		{
			var output = CallCommand("!help");
			Assert.AreEqual(output, CallCommand("!h"));
			Assert.AreEqual(output, CallCommand("!eval h"));
			Assert.AreEqual(output, CallCommand("!(!h)"));
			output = CallCommand("!h help");
			Assert.AreEqual(output, CallCommand("!(!h) h"));
			Assert.Throws<CommandException>(() => CallCommand("!"));

			// Test random
			for (int i = 0; i < 1000; i++)
			{
				var r = int.Parse(CallCommand("!rng -10 100"));
				Assert.GreaterOrEqual(r, -10);
				Assert.Less(r, 100);
			}

			// Take
			Assert.Throws<CommandException>(() => CallCommand("!take"));
			Assert.AreEqual("text", CallCommand("!take 1 text"));
			Assert.Throws<CommandException>(() => CallCommand("!take 2 text"));
			Assert.Throws<CommandException>(() => CallCommand("!take -1 text"));
			Assert.AreEqual("no", CallCommand("!take 1 \"no more text\""));
			Assert.AreEqual("no more", CallCommand("!take 2 \"no more text\""));
			Assert.AreEqual("more", CallCommand("!take 1 1 \"no more text\""));
			Assert.AreEqual("more text", CallCommand("!take 2 1 \"no more text\""));
			Assert.Throws<CommandException>(() => CallCommand("!take 2 -1 \"no more text\""));
			Assert.AreEqual("te", CallCommand("!take 1 0 x text"));
			Assert.AreEqual("t", CallCommand("!take 1 1 x text"));
			Assert.AreEqual("text", CallCommand("!take 1 0 z text"));
			Assert.Throws<CommandException>(() => CallCommand("!take 1 1 z text"));
			Assert.AreEqual("", CallCommand("!take 0 text"));
			Assert.AreEqual("", CallCommand("!take 0 0 text"));
			Assert.AreEqual("", CallCommand("!take 0 0 z text"));

			// If
			Assert.Throws<CommandException>(() => CallCommand("!if a == a"));
			Assert.Throws<CommandException>(() => CallCommand("!if a == b"));
			Assert.AreEqual("text", CallCommand("!if a == a text"));
			Assert.IsNull(CallCommand("!if a == b text"));
			Assert.AreEqual("other", CallCommand("!if a == b text other"));
			Assert.AreEqual("text", CallCommand("!if 1 == 1 text other"));
			Assert.AreEqual("other", CallCommand("!if 1 == 2 text other"));
			Assert.AreEqual("text", CallCommand("!if 1.0 == 1 text other"));
			Assert.AreEqual("other", CallCommand("!if 1.0 == 1.1 text other"));
			Assert.AreEqual("text", CallCommand("!if a == a text (!)"));
			Assert.Throws<CommandException>(() => CallCommand("!if a == b text (!)"));
		}
	}
}
