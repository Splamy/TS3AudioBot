// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3ABotUnitTests
{
	using NUnit.Framework;

	using TS3AudioBot;
	using TS3AudioBot.CommandSystem;

	[TestFixture]
	public class BotCommandTests
	{
		private readonly CommandManager cmdMgr;

		public BotCommandTests()
		{
			cmdMgr = new CommandManager();
			cmdMgr.RegisterMain();
			Utils.ExecInfo.AddDynamicObject(cmdMgr);
		}

		private string CallCommand(string command)
		{
			return cmdMgr.CommandSystem.ExecuteCommand(Utils.ExecInfo, command);
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

	static class Utils
	{
		static Utils()
		{
			ExecInfo = new ExecutionInformation();
			ExecInfo.AddDynamicObject(new CallerInfo(null, false) { SkipRightsChecks = true });
			ExecInfo.AddDynamicObject(new InvokerData("InvokerUid"));
		}

		public static ExecutionInformation ExecInfo { get; }
	}
}
