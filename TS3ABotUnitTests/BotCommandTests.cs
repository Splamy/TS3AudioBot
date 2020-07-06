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
using TS3AudioBot.CommandSystem.Ast;
using TS3AudioBot.CommandSystem.Commands;
using TS3AudioBot.Dependency;
using TS3AudioBot.Web.Api;
using TSLib;

#nullable enable
namespace TS3ABotUnitTests
{
	[TestFixture]
	public class BotCommandTests
	{
		[Test]
		public void BotCommandTest()
		{
			var execInfo = Utils.GetExecInfo("ic3");
			string? CallCommand(string command) => CommandManager.Execute(execInfo, command).GetAwaiter().GetResult().AsString();

			var output = CallCommand("!help");
			Assert.AreEqual(output, CallCommand("!h"));
			Assert.AreEqual(output, CallCommand("!eval !h"));
			Assert.Throws<CommandException>(() => CallCommand("!"));

			// Test random
			for (int i = 0; i < 1000; i++)
			{
				var r = int.Parse(CallCommand("!rng -10 100")!);
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
		public void TailStringTest()
		{
			var execInfo = Utils.GetExecInfo("ic3");
			string? CallCommand(string command) => CommandManager.Execute(execInfo, command).Result.AsString();
			var group = execInfo.GetModule<CommandManager>()!.RootGroup;
			group.AddCommand("cmd", new FunctionCommand(s => s));

			Assert.AreEqual("a", CallCommand("!cmd a"));
			Assert.AreEqual("a b", CallCommand("!cmd a b"));
			Assert.AreEqual("a", CallCommand("!cmd a \" b"));
			Assert.AreEqual("a b 1", CallCommand("!cmd a b 1"));
		}

		[Test]
		public void XCommandSystemFilterTest()
		{
			var filterList = new Dictionary<string, object?>
			{
				{ "help", null },
				{ "quit", null },
				{ "play", null },
				{ "ply", null }
			};

			var filter = Filter.GetFilterByName("ic3")!;

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

		private static string OptionalFunc(string? s = null) => s is null ? "NULL" : "NOT NULL";

		[Test]
		public void XCommandSystemTest()
		{
			var execInfo = Utils.GetExecInfo("ic3", false);
			string? CallCommand(string command) => CommandManager.Execute(execInfo, command).GetAwaiter().GetResult().AsString();

			var group = execInfo.GetModule<CommandManager>()!.RootGroup;
			group.AddCommand("one", new FunctionCommand(() => "ONE"));
			group.AddCommand("two", new FunctionCommand(() => "TWO"));
			group.AddCommand("echo", new FunctionCommand(s => s));
			group.AddCommand("optional", new FunctionCommand(GetType().GetMethod(nameof(OptionalFunc), BindingFlags.NonPublic | BindingFlags.Static)!));

			// Basic tests
			Assert.AreEqual("ONE", CommandManager.Execute(execInfo, new ICommand[] { new ResultCommand("one") }).Result.AsString());
			Assert.AreEqual("ONE", CallCommand("!one"));
			Assert.AreEqual("TWO", CallCommand("!t"));
			Assert.AreEqual("TEST", CallCommand("!e TEST"));
			Assert.AreEqual("ONE", CallCommand("!o"));

			// Optional parameters
			Assert.Throws<CommandException>(() => CallCommand("!e"));
			Assert.AreEqual("NULL", CallCommand("!op"));
			Assert.AreEqual("NOT NULL", CallCommand("!op 1"));

			// Command chaining
			Assert.AreEqual("TEST", CallCommand("!e (!e TEST)"));
			Assert.AreEqual("TWO", CallCommand("!e (!t)"));
			Assert.AreEqual("NOT NULL", CallCommand("!op (!e TEST)"));
			Assert.AreEqual("ONE", CallCommand("!(!e on)"));

			// Command overloading
			var intCom = new Func<int, string>(_ => "INT");
			var strCom = new Func<string, string>(_ => "STRING");
			group.AddCommand("overlord", new OverloadedFunctionCommand(new[] {
				new FunctionCommand(intCom.Method, intCom.Target),
				new FunctionCommand(strCom.Method, strCom.Target)
			}));

			Assert.AreEqual("INT", CallCommand("!overlord 1"));
			Assert.AreEqual("STRING", CallCommand("!overlord a"));
			Assert.Throws<CommandException>(() => CallCommand("!overlord"));

			// Return unwrap
			var json = JsonValue.Create("WRAP");
			group.AddCommand("wrapjson", new FunctionCommand(new Func<JsonValue>(() => json)));
			Assert.AreEqual(json, CommandManager.Execute(execInfo, "!wrapjson").Result.AsRaw());
			Assert.AreEqual("WRAP", CallCommand("!wrapjson")); // AsString()
			Assert.AreEqual("WRAP", CallCommand("!echo (!wrapjson)"));
		}

		[Test]
		public void XCommandSystemTest2()
		{
			var execInfo = Utils.GetExecInfo("exact");
			string? CallCommand(string command) => CommandManager.Execute(execInfo!, command).GetAwaiter().GetResult().AsString();
			var group = execInfo.GetModule<CommandManager>()!.RootGroup;

			var o1 = new OverloadedFunctionCommand();
			o1.AddCommand(new FunctionCommand(new Action<int>((_) => { })));
			o1.AddCommand(new FunctionCommand(new Action<long>((_) => { })));
			group.AddCommand("one", o1);

			group.AddCommand("two", new FunctionCommand(new Action<StringSplitOptions>((_) => { })));

			var o2 = new CommandGroup();
			o2.AddCommand("a", new FunctionCommand(new Action(() => { })));
			o2.AddCommand("b", new FunctionCommand(new Action(() => { })));
			group.AddCommand("three", o2);

			Assert.Throws<CommandException>(() => CallCommand("!one"));
			Assert.Throws<CommandException>(() => CallCommand("!one \"\""));
			Assert.Throws<CommandException>(() => CallCommand("!one (!print \"\")"));
			Assert.Throws<CommandException>(() => CallCommand("!one string"));
			Assert.DoesNotThrow(() => CallCommand("!one 42"));
			Assert.DoesNotThrow(() => CallCommand("!one 4200000000000"));

			Assert.Throws<CommandException>(() => CallCommand("!two"));
			Assert.Throws<CommandException>(() => CallCommand("!two \"\""));
			Assert.Throws<CommandException>(() => CallCommand("!two (!print \"\")"));
			Assert.Throws<CommandException>(() => CallCommand("!two 42"));
			Assert.DoesNotThrow(() => CallCommand("!two None"));

			Assert.Throws<CommandException>(() => CallCommand("!three"));
			Assert.Throws<CommandException>(() => CallCommand("!three \"\""));
			Assert.Throws<CommandException>(() => CallCommand("!three (!print \"\")"));
			Assert.Throws<CommandException>(() => CallCommand("!three c"));
			Assert.DoesNotThrow(() => CallCommand("!three a"));
			Assert.DoesNotThrow(() => CallCommand("!three b"));
		}

		[Test]
		public void EnsureAllCommandsHaveEnglishDocumentationEntry()
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en");
			Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en");

			var execInfo = Utils.GetExecInfo("exact");
			var cmdMgr = execInfo.GetModule<CommandManager>()!;
			var errors = new List<string>();
			foreach (var cmd in cmdMgr.AllCommands)
			{
				if (string.IsNullOrEmpty(cmd.Description))
					errors.Add($"Command {cmd.FullQualifiedName} has no documentation");
			}
			if (errors.Count > 0)
				Assert.Fail(string.Join("\n", errors));
		}

		[Test]
		public void CommandParserTest()
		{
			TestStringParsing("!aaa", "aaa");
			TestStringParsing("!a\"aa", "a\"aa");
			TestStringParsing("!aaa\"", "aaa\"");
			TestStringParsing("!a'aa", "a'aa");
			TestStringParsing("!aaa'", "aaa'");
			TestStringParsing("!\"aaa\"", "aaa");
			TestStringParsing("!\"aaa", "aaa");
			TestStringParsing("!'aaa'", "aaa");
			TestStringParsing("!'aaa", "aaa");
			TestStringParsing("!\"a\"aa\"", "a");
			TestStringParsing("!'a'aa'", "a");
			TestStringParsing("!\"a'aa\"", "a'aa");
			TestStringParsing("!'a\"aa'", "a\"aa");
			TestStringParsing("!\"a\\'aa\"", "a\\'aa");
			TestStringParsing("!\"a\\\"aa\"", "a\"aa");
			TestStringParsing("!'a\\'aa'", "a'aa");
			TestStringParsing("!'a\\\"aa'", "a\\\"aa");
		}

		public static void TestStringParsing(string inp, string outp)
		{
			var astc = CommandParser.ParseCommandRequest(inp);
			var ast = ((AstCommand)astc).Parameter[0];
			Assert.AreEqual(outp, ((AstValue)ast).Value);
		}
	}

	internal static class Utils
	{
		public static ExecutionInformation GetExecInfo(string matcher, bool addMainCommands = true)
		{
			var cmdMgr = new CommandManager(null!);
			if (addMainCommands)
				cmdMgr.RegisterCollection(MainCommands.Bag);

			var execInfo = new ExecutionInformation();
			execInfo.AddModule(new CallerInfo(false) { SkipRightsChecks = true, CommandComplexityMax = int.MaxValue });
			execInfo.AddModule(new InvokerData((Uid)"InvokerUid"));
			execInfo.AddModule(Filter.GetFilterByName(matcher) ?? throw new Exception("Test filter not found"));
			execInfo.AddModule(cmdMgr);
			return execInfo;
		}
	}
}
