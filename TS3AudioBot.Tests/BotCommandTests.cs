using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot;
using TS3AudioBot.Algorithm;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.CommandSystem.Ast;
using TS3AudioBot.CommandSystem.Commands;
using TS3AudioBot.Dependency;
using TS3AudioBot.Web.Api;
using TSLib;

#nullable enable
namespace TS3AudioBot.Tests;

[SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly",
	Justification = "Unit tests here are for non-async operations")]
public class BotCommandTests
{
	private static string? CmdSync(ExecutionInformation info, string command)
	{
		var valueTask = CommandManager.Execute(info, command);
		if (valueTask.IsCompleted)
			return valueTask.GetAwaiter().GetResult().AsString();
		Assert.Fail("Cannot test with async task");
		return null;
	}

	[Fact]
	public void BotCommandTest()
	{
		var execInfo = Utils.GetExecInfo("ic3");
		string? CallCommand(string command) => CmdSync(execInfo, command);

		var output = CallCommand("!help");
		Assert.Equal(output, CallCommand("!h"));
		Assert.Equal(output, CallCommand("!eval !h"));
		Assert.Throws<CommandException>(() => CallCommand("!"));

		// Test random
		for (int i = 0; i < 1000; i++)
		{
			var r = int.Parse(CallCommand("!rng -10 100")!);
			Assert.InRange(r, -10, 99);
		}

		// Take
		Assert.Throws<CommandException>(() => CallCommand("!take"));
		Assert.Equal("text", CallCommand("!take 1 text"));
		Assert.Throws<CommandException>(() => CallCommand("!take 2 text"));
		Assert.Throws<CommandException>(() => CallCommand("!take -1 text"));
		Assert.Equal("no", CallCommand("!take 1 \"no more text\""));
		Assert.Equal("no more", CallCommand("!take 2 \"no more text\""));
		Assert.Equal("more", CallCommand("!take 1 1 \"no more text\""));
		Assert.Equal("more text", CallCommand("!take 2 1 \"no more text\""));
		Assert.Throws<CommandException>(() => CallCommand("!take 2 -1 \"no more text\""));
		Assert.Equal("te", CallCommand("!take 1 0 x text"));
		Assert.Equal("t", CallCommand("!take 1 1 x text"));
		Assert.Equal("text", CallCommand("!take 1 0 z text"));
		Assert.Throws<CommandException>(() => CallCommand("!take 1 1 z text"));
		Assert.Equal("", CallCommand("!take 0 text"));
		Assert.Equal("", CallCommand("!take 0 0 text"));
		Assert.Equal("", CallCommand("!take 0 0 z text"));

		// If
		Assert.Throws<CommandException>(() => CallCommand("!if a == a"));
		Assert.Throws<CommandException>(() => CallCommand("!if a == b"));
		Assert.Equal("text", CallCommand("!if a == a text"));
		Assert.Null(CallCommand("!if a == b text"));
		Assert.Equal("other", CallCommand("!if a == b text other"));
		Assert.Equal("text", CallCommand("!if 1 == 1 text other"));
		Assert.Equal("other", CallCommand("!if 1 == 2 text other"));
		Assert.Equal("text", CallCommand("!if 1.0 == 1 text other"));
		Assert.Equal("other", CallCommand("!if 1.0 == 1.1 text other"));
		Assert.Equal("text", CallCommand("!if a == a text (!)"));
		Assert.Throws<CommandException>(() => CallCommand("!if a == b text (!)"));
	}

	[Fact]
	public void TailStringTest()
	{
		var execInfo = Utils.GetExecInfo("ic3");
		string? CallCommand(string command) => CmdSync(execInfo, command);
		var group = execInfo.GetModuleOrThrow<CommandManager>().RootGroup;
		group.AddCommand("cmd", new FunctionCommand(s => s));

		Assert.Equal("a", CallCommand("!cmd a"));
		Assert.Equal("a b", CallCommand("!cmd a b"));
		Assert.Equal("a", CallCommand("!cmd a \" b"));
		Assert.Equal("a b 1", CallCommand("!cmd a b 1"));
	}

	[Fact]
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
		Assert.Equal("help", Assert.Single(result).Key);

		// The first occurrence of y
		result = filter.Filter(filterList, "y");
		Assert.Equal("ply", Assert.Single(result).Key);

		// The smallest word
		result = filter.Filter(filterList, "zorn");
		Assert.Equal("ply", Assert.Single(result).Key);

		// First letter match
		result = filter.Filter(filterList, "q");
		Assert.Equal("quit", Assert.Single(result).Key);

		// Ignore other letters
		result = filter.Filter(filterList, "palyndrom");
		Assert.Equal("play", Assert.Single(result).Key);

		filterList.Add("pla", null);

		// Ambiguous command
		result = filter.Filter(filterList, "p");
		Assert.Equal(2, result.Count());
		Assert.Contains(result, r => r.Key == "ply");
		Assert.Contains(result, r => r.Key == "pla");
	}

	private static string OptionalFunc(string? s = null) => s is null ? "NULL" : "NOT NULL";

	[Fact]
	public async Task XCommandSystemTest()
	{
		var execInfo = Utils.GetExecInfo("ic3", false);
		string? CallCommand(string command) => CmdSync(execInfo, command);
		var group = execInfo.GetModuleOrThrow<CommandManager>().RootGroup;

		group.AddCommand("one", new FunctionCommand(() => "ONE"));
		group.AddCommand("two", new FunctionCommand(() => "TWO"));
		group.AddCommand("echo", new FunctionCommand(s => s));
		group.AddCommand("optional",
			new FunctionCommand(
				GetType().GetMethod(nameof(OptionalFunc), BindingFlags.NonPublic | BindingFlags.Static)!));

		// Basic tests
		Assert.Equal("ONE", (await CommandManager.Execute(execInfo, [new ResultCommand("one")])).AsString());
		Assert.Equal("ONE", CallCommand("!one"));
		Assert.Equal("TWO", CallCommand("!t"));
		Assert.Equal("TEST", CallCommand("!e TEST"));
		Assert.Equal("ONE", CallCommand("!o"));

		// Optional parameters
		Assert.Throws<CommandException>(() => CallCommand("!e"));
		Assert.Equal("NULL", CallCommand("!op"));
		Assert.Equal("NOT NULL", CallCommand("!op 1"));

		// Command chaining
		Assert.Equal("TEST", CallCommand("!e (!e TEST)"));
		Assert.Equal("TWO", CallCommand("!e (!t)"));
		Assert.Equal("NOT NULL", CallCommand("!op (!e TEST)"));
		Assert.Equal("ONE", CallCommand("!(!e on)"));

		// Command overloading
		var intCom = new Func<int, string>(_ => "INT");
		var strCom = new Func<string, string>(_ => "STRING");
		group.AddCommand("overlord", new OverloadedFunctionCommand([
			new FunctionCommand(intCom.Method, intCom.Target),
			new FunctionCommand(strCom.Method, strCom.Target)
		]));

		Assert.Equal("INT", CallCommand("!overlord 1"));
		Assert.Equal("STRING", CallCommand("!overlord a"));
		Assert.Throws<CommandException>(() => CallCommand("!overlord"));

		// Return unwrap
		var json = JsonValue.Create("WRAP");
		group.AddCommand("wrapjson", new FunctionCommand(new Func<JsonValue>(() => json)));
		Assert.Equal(json, (await CommandManager.Execute(execInfo, "!wrapjson")).AsRaw());
		Assert.Equal("WRAP", CallCommand("!wrapjson")); // AsString()
		Assert.Equal("WRAP", CallCommand("!echo (!wrapjson)"));
	}

	[Fact]
	public void XCommandSystemTest2()
	{
		var execInfo = Utils.GetExecInfo("exact");
		string? CallCommand(string command) => CmdSync(execInfo, command);
		var group = execInfo.GetModuleOrThrow<CommandManager>().RootGroup;

		var o1 = new OverloadedFunctionCommand();
		o1.AddCommand(new FunctionCommand(new Action<int>((_) => { })));
		o1.AddCommand(new FunctionCommand(new Action<long>((_) => { })));
		group.AddCommand("one", o1);

		group.AddCommand("two", new FunctionCommand(new Action<StringSplitOptions>((_) => { })));

		var o2 = new CommandGroup();
		o2.AddCommand("a", new FunctionCommand(() => { }));
		o2.AddCommand("b", new FunctionCommand(() => { }));
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

	[Fact]
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

	[Fact]
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

	private static void TestStringParsing(string inp, string outp)
	{
		var astc = CommandParser.ParseCommandRequest(inp);
		var ast = ((AstCommand)astc).Parameter[0];
		Assert.Equal(outp, ((AstValue)ast).Value);
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
