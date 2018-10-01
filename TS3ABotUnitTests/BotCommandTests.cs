// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3ABotUnitTests
{
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Reflection;
	using System.Threading;
	using TS3AudioBot;
	using TS3AudioBot.Algorithm;
	using TS3AudioBot.CommandSystem;
	using TS3AudioBot.CommandSystem.CommandResults;
	using TS3AudioBot.CommandSystem.Commands;

	[TestFixture]
	public class BotCommandTests
	{
		private readonly CommandManager cmdMgr;

		public BotCommandTests()
		{
			cmdMgr = new CommandManager();
			cmdMgr.RegisterCollection(MainCommands.Bag);
			Utils.ExecInfo.AddDynamicObject(cmdMgr);
		}

		private string CallCommand(string command)
		{
			return cmdMgr.CommandSystem.ExecuteCommand(Utils.ExecInfo, command);
		}

		[Test]
		public void BotCommandTest()
		{
			Utils.FilterBy("ic3");
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

		[Test]
		public void XCommandSystemFilterTest()
		{
			var filterList = new Dictionary<string, object>
			{
				{ "help", null },
				{ "quit", null },
				{ "play", null },
				{ "ply", null }
			};

			var filter = Filter.GetFilterByName("ic3").Unwrap();

			// Exact match
			var result = filter.Filter(filterList, "help");
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("help", result.First().Key);

			// The first occurence of y
			result = filter.Filter(filterList, "y");
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("ply", result.First().Key);

			// The smallest word
			result = filter.Filter(filterList, "zorn");
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("ply", result.First().Key);

			// First letter match
			result = filter.Filter(filterList, "q");
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("quit", result.First().Key);

			// Ignore other letters
			result = filter.Filter(filterList, "palyndrom");
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("play", result.First().Key);

			filterList.Add("pla", null);

			// Ambiguous command
			result = filter.Filter(filterList, "p");
			Assert.AreEqual(2, result.Count());
			Assert.IsTrue(result.Any(r => r.Key == "ply"));
			Assert.IsTrue(result.Any(r => r.Key == "pla"));
		}

		private static string OptionalFunc(string s = null) => s is null ? "NULL" : "NOT NULL";

		[Test]
		public void XCommandSystemTest()
		{
			Utils.FilterBy("ic3");
			var commandSystem = new XCommandSystem();
			var group = commandSystem.RootCommand;
			group.AddCommand("one", new FunctionCommand(() => "ONE"));
			group.AddCommand("two", new FunctionCommand(() => "TWO"));
			group.AddCommand("echo", new FunctionCommand(s => s));
			group.AddCommand("optional", new FunctionCommand(GetType().GetMethod(nameof(OptionalFunc), BindingFlags.NonPublic | BindingFlags.Static)));

			// Basic tests
			Assert.AreEqual("ONE", ((StringCommandResult)commandSystem.Execute(Utils.ExecInfo,
				 new ICommand[] { new StringCommand("one") })).Content);
			Assert.AreEqual("ONE", commandSystem.ExecuteCommand(Utils.ExecInfo, "!one"));
			Assert.AreEqual("TWO", commandSystem.ExecuteCommand(Utils.ExecInfo, "!t"));
			Assert.AreEqual("TEST", commandSystem.ExecuteCommand(Utils.ExecInfo, "!e TEST"));
			Assert.AreEqual("ONE", commandSystem.ExecuteCommand(Utils.ExecInfo, "!o"));

			// Optional parameters
			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!e"));
			Assert.AreEqual("NULL", commandSystem.ExecuteCommand(Utils.ExecInfo, "!op"));
			Assert.AreEqual("NOT NULL", commandSystem.ExecuteCommand(Utils.ExecInfo, "!op 1"));

			// Command chaining
			Assert.AreEqual("TEST", commandSystem.ExecuteCommand(Utils.ExecInfo, "!e (!e TEST)"));
			Assert.AreEqual("TWO", commandSystem.ExecuteCommand(Utils.ExecInfo, "!e (!t)"));
			Assert.AreEqual("NOT NULL", commandSystem.ExecuteCommand(Utils.ExecInfo, "!op (!e TEST)"));
			Assert.AreEqual("ONE", commandSystem.ExecuteCommand(Utils.ExecInfo, "!(!e on)"));

			// Command overloading
			var intCom = new Func<int, string>(_ => "INT");
			var strCom = new Func<string, string>(_ => "STRING");
			group.AddCommand("overlord", new OverloadedFunctionCommand(new[] {
				new FunctionCommand(intCom.Method, intCom.Target),
				new FunctionCommand(strCom.Method, strCom.Target)
			}));

			Assert.AreEqual("INT", commandSystem.ExecuteCommand(Utils.ExecInfo, "!overlord 1"));
			Assert.AreEqual("STRING", commandSystem.ExecuteCommand(Utils.ExecInfo, "!overlord a"));
			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!overlord"));
		}

		[Test]
		public void XCommandSystemTest2()
		{
			Utils.FilterBy("exact");
			var commandSystem = new XCommandSystem();
			var group = commandSystem.RootCommand;

			var o1 = new OverloadedFunctionCommand();
			o1.AddCommand(new FunctionCommand(new Action<int>((_) => { })));
			o1.AddCommand(new FunctionCommand(new Action<long>((_) => { })));
			group.AddCommand("one", o1);

			group.AddCommand("two", new FunctionCommand(new Action<StringSplitOptions>((_) => { })));

			var o2 = new CommandGroup();
			o2.AddCommand("a", new FunctionCommand(new Action(() => { })));
			o2.AddCommand("b", new FunctionCommand(new Action(() => { })));
			group.AddCommand("three", o2);

			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!one"));
			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!one \"\""));
			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!one (!print \"\")"));
			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!one string"));
			Assert.DoesNotThrow(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!one 42"));
			Assert.DoesNotThrow(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!one 4200000000000"));

			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!two"));
			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!two \"\""));
			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!two (!print \"\")"));
			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!two 42"));
			Assert.DoesNotThrow(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!two None"));

			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!three"));
			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!three \"\""));
			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!three (!print \"\")"));
			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!three c"));
			Assert.DoesNotThrow(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!three a"));
			Assert.DoesNotThrow(() => commandSystem.ExecuteCommand(Utils.ExecInfo, "!three b"));
		}

		[Test]
		public void EnsureAllCommandsHaveEnglishDocumentationEntry()
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en");
			Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en");

			foreach (var cmd in cmdMgr.AllCommands)
			{
				Assert.IsFalse(string.IsNullOrEmpty(cmd.Description), $"Command {cmd.FullQualifiedName} has no documentation");
			}
		}
	}

	internal static class Utils
	{
		private static readonly Filter filter = new Filter();

		static Utils()
		{
			ExecInfo = new ExecutionInformation();
			ExecInfo.AddDynamicObject(new CallerInfo(null, false) { SkipRightsChecks = true });
			ExecInfo.AddDynamicObject(new InvokerData("InvokerUid"));
			ExecInfo.AddDynamicObject(filter);
		}

		public static void FilterBy(string name)
		{
			filter.Current = Filter.GetFilterByName(name).Unwrap();
		}

		public static ExecutionInformation ExecInfo { get; }
	}
}
