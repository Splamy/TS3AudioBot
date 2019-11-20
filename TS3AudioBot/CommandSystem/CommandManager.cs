// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TS3AudioBot.CommandSystem.Ast;
using TS3AudioBot.CommandSystem.CommandResults;
using TS3AudioBot.CommandSystem.Commands;
using TS3AudioBot.CommandSystem.Text;
using TS3AudioBot.Dependency;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;
using TS3AudioBot.Rights;
using TSLib.Helper;
using static TS3AudioBot.CommandSystem.CommandSystemTypes;

namespace TS3AudioBot.CommandSystem
{
	/// <summary>Mangement for the bot command system.</summary>
	public class CommandManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Regex CommandNamespaceValidator =
			new Regex(@"^[a-z\d]+( [a-z\d]+)*$", Util.DefaultRegexConfig & ~RegexOptions.IgnoreCase);

		private readonly Dictionary<string, AliasCommand> aliasPaths = new Dictionary<string, AliasCommand>();
		private readonly HashSet<string> commandPaths = new HashSet<string>();
		private readonly HashSet<ICommandBag> baggedCommands = new HashSet<ICommandBag>();
		private readonly RightsManager rightsManager;

		public RootGroup RootGroup { get; } = new RootGroup();

		public CommandManager(RightsManager rightsManager)
		{
			this.rightsManager = rightsManager;
		}

		public IEnumerable<BotCommand> AllCommands => baggedCommands.SelectMany(x => x.BagCommands);

		public IEnumerable<string> AllRights => AllCommands.Select(x => x.RequiredRight).Concat(baggedCommands.SelectMany(x => x.AdditionalRights));

		#region Management

		public void RegisterCollection(ICommandBag bag)
		{
			if (baggedCommands.Contains(bag))
				return;

			CheckDistinct(bag.BagCommands);
			baggedCommands.Add(bag);

			foreach (var command in bag.BagCommands)
			{
				var result = LoadCommand(command);
				if (!result.Ok)
				{
					Log.Error("Failed to load command bag: " + result.Error);
					UnregisterCollection(bag);
					throw new InvalidOperationException(result.Error);
				}
			}
			rightsManager?.SetRightsList(AllRights);
		}

		public void UnregisterCollection(ICommandBag bag)
		{
			if (baggedCommands.Remove(bag))
			{
				foreach (var com in bag.BagCommands)
				{
					UnloadCommand(com);
				}
				rightsManager?.SetRightsList(AllRights);
			}
		}

		public E<LocalStr> RegisterAlias(string path, string command)
		{
			if (aliasPaths.ContainsKey(path))
				return new LocalStr("Already exists"); // TODO

			var dac = new AliasCommand(command);
			var res = LoadICommand(dac, path);
			if (!res)
				return new LocalStr(res.Error); // TODO

			aliasPaths.Add(path, dac);
			return R.Ok;
		}

		public E<LocalStr> UnregisterAlias(string path)
		{
			if (!aliasPaths.TryGetValue(path, out var com))
				return new LocalStr("Does not exist"); // TODO

			UnloadICommand(com, path);

			aliasPaths.Remove(path);

			return R.Ok;
		}

		public IEnumerable<string> AllAlias => aliasPaths.Keys;

		public AliasCommand GetAlias(string path) => aliasPaths.TryGetValue(path, out var ali) ? ali : null;

		public static IEnumerable<BotCommand> GetBotCommands(object obj, Type type = null) => GetBotCommands(GetCommandMethods(obj, type));

		public static IEnumerable<BotCommand> GetBotCommands(IEnumerable<CommandBuildInfo> methods)
		{
			foreach (var botData in methods)
			{
				botData.UsageList = botData.Method.GetCustomAttributes<UsageAttribute>().ToArray();
				yield return new BotCommand(botData);
			}
		}

		public static IEnumerable<CommandBuildInfo> GetCommandMethods(object obj, Type type = null)
		{
			if (obj is null && type is null)
				throw new ArgumentNullException(nameof(type), "No type information given.");
			return GetCommandMethodsIterator();
			IEnumerable<CommandBuildInfo> GetCommandMethodsIterator()
			{
				var objType = type ?? obj.GetType();

				foreach (var method in objType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
				{
					var comAtt = method.GetCustomAttribute<CommandAttribute>();
					if (comAtt is null) continue;
					if (obj is null && !method.IsStatic)
					{
						Log.Warn("Method '{0}' needs an instance, but no instance was provided. It will be ignored.", method.Name);
						continue;
					}
					yield return new CommandBuildInfo(obj, method, comAtt);
				}
			}
		}

		private static void CheckDistinct(IReadOnlyCollection<BotCommand> list)
		{
			if (list.Select(c => c.FullQualifiedName).Distinct().Count() < list.Count)
			{
				var duplicates = list.GroupBy(c => c.FullQualifiedName).Where(g => g.Count() > 1).Select(g => g.Key);
				throw new InvalidOperationException("The object contains duplicates: " + string.Join(", ", duplicates));
			}
		}

		private E<string> LoadCommand(BotCommand com)
		{
			if (commandPaths.Contains(com.FullQualifiedName))
				return "Command already exists: " + com.InvokeName;

			commandPaths.Add(com.FullQualifiedName);
			return LoadICommand(com, com.InvokeName);
		}

		private E<string> LoadICommand(ICommand com, string path)
		{
			if (!CommandNamespaceValidator.IsMatch(path))
				return "Command has an invalid invoke name: " + path;

			string[] comPath = path.Split(' ');

			var buildResult = BuildAndGet(comPath.Take(comPath.Length - 1));
			if (!buildResult)
				return GenerateError(buildResult.Error, com as BotCommand);

			var result = InsertInto(buildResult.Value, com, comPath.Last());
			if (!result)
				return GenerateError(result.Error, com as BotCommand);

			return R.Ok;
		}

		private R<CommandGroup, string> BuildAndGet(IEnumerable<string> comPath)
		{
			CommandGroup group = RootGroup;
			// this for loop iterates through the separate names of
			// the command to be added.
			foreach (var comPathPart in comPath)
			{
				switch (group.GetCommand(comPathPart))
				{
				// if a group to hold the next level command doesn't exist
				// it will be created here
				case null:
					var nextGroup = new CommandGroup();
					group.AddCommand(comPathPart, nextGroup);
					group = nextGroup;
					break;

				// if the group already exists we can take it.
				case CommandGroup cgCommand:
					group = cgCommand;
					break;

				// if the element is anything else, we have to replace it
				// with a group and put the old element back into it.
				case FunctionCommand fnCommand:
					var subGroup = new CommandGroup();
					group.RemoveCommand(comPathPart);
					group.AddCommand(comPathPart, subGroup);
					var insertResult = InsertInto(group, fnCommand, comPathPart);
					if (!insertResult.Ok)
						return insertResult.Error;
					group = subGroup;
					break;

				default:
					return "An overloaded command cannot be replaced by a CommandGroup";
				}
			}

			return group;
		}

		private static E<string> InsertInto(CommandGroup group, ICommand com, string name)
		{
			var subCommand = group.GetCommand(name);

			switch (subCommand)
			{
			case null:
				// the group we are trying to insert has no element with the current
				// name, so just insert it
				group.AddCommand(name, com);
				return R.Ok;

			case CommandGroup insertCommand:
				// to add a command to CommandGroup will have to treat it as a subcommand
				// with an empty string as a name
				var noparamCommand = insertCommand.GetCommand(string.Empty);
				if (noparamCommand is null)
				{
					insertCommand.AddCommand(string.Empty, com);
					if (com is BotCommand botCom && botCom.NormalParameters > 0)
						Log.Warn("\"{0}\" has at least one parameter and won't be reachable due to an overloading function.", botCom.FullQualifiedName);
					return R.Ok;
				}
				else
					return "An empty named function under a group cannot be overloaded.";
			}

			if (!(com is FunctionCommand funcCom))
				return $"The command cannot be inserted into a complex node ({name}).";

			switch (subCommand)
			{
			case FunctionCommand subFuncCommand:
				// if we have is a simple function, we need to create a overloader
				// and then add both functions to it
				group.RemoveCommand(name);
				var overloader = new OverloadedFunctionCommand();
				overloader.AddCommand(subFuncCommand);
				overloader.AddCommand(funcCom);
				group.AddCommand(name, overloader);
				break;

			case OverloadedFunctionCommand insertCommand:
				// if we have a overloaded function, we can simply add it
				insertCommand.AddCommand(funcCom);
				break;

			default:
				return "Unknown node to insert to.";
			}

			return R.Ok;
		}

		private static E<string> GenerateError(string msg, BotCommand involvedCom)
		{
			return $"Command error path: {involvedCom?.InvokeName}"
				+ $"Command: {involvedCom?.FullQualifiedName}"
				+ $"Error: {msg}";
		}

		private void UnloadCommand(BotCommand com)
		{
			if (!commandPaths.Remove(com.FullQualifiedName))
				return;

			UnloadICommand(com, com.InvokeName);
		}

		private void UnloadICommand(ICommand com, string path)
		{
			var comPath = path.Split(' ');

			var node = new CommandUnloadNode
			{
				ParentNode = null,
				Self = RootGroup,
			};

			// build up the list to our desired node
			for (int i = 0; i < comPath.Length - 1; i++)
			{
				if (!(node.Self.GetCommand(comPath[i]) is CommandGroup nextGroup))
					break;

				node = new CommandUnloadNode
				{
					ParentNode = node,
					Self = nextGroup,
				};
			}

			var subGroup = node.Self.GetCommand(comPath.Last());

			switch (subGroup)
			{
			// nothing to remove
			case null:
				return;
			// if the subnode is a plain FunctionCommand then we found our command to delete
			case FunctionCommand _:
			case AliasCommand _:
				node.Self.RemoveCommand(com);
				break;
			// here we can delete our command from the overloader
			case OverloadedFunctionCommand subOverloadGroup:
				if (com is FunctionCommand funcCom)
					subOverloadGroup.RemoveCommand(funcCom);
				else
					return;
				break;
			// now to the special case when a command gets inserted with an empty string
			case CommandGroup insertGroup:
				// since we check precisely that only one command and only a simple FunctionCommand
				// can be added with an empty string, wen can delete it safely this way
				insertGroup.RemoveCommand(string.Empty);
				// add the node for cleanup
				node = new CommandUnloadNode
				{
					ParentNode = node,
					Self = insertGroup,
				};
				break;
			}

			// and finally clean all empty nodes up
			while (node != null)
			{
				if (node.Self.IsEmpty)
					node.ParentNode?.Self.RemoveCommand(node.Self);
				node = node.ParentNode;
			}
		}

		private class CommandUnloadNode
		{
			public CommandUnloadNode ParentNode { get; set; }
			public CommandGroup Self { get; set; }
		}

		#endregion

		#region Execution

		internal static ICommand AstToCommandResult(AstNode node)
		{
			switch (node.Type)
			{
			case AstType.Error:
				throw new CommandException("Found an unconvertable ASTNode of type Error", CommandExceptionReason.InternalError);
			case AstType.Command:
				var cmd = (AstCommand)node;
				var arguments = new ICommand[cmd.Parameter.Count];
				int tailCandidates = 0;
				for (int i = cmd.Parameter.Count - 1; i >= 1; i--)
				{
					var para = cmd.Parameter[i];
					if (!(para is AstValue astVal) || astVal.StringType != StringType.FreeString)
						break;

					arguments[i] = new TailStringAutoConvertCommand(new TailString(astVal.Value, astVal.TailString));
					tailCandidates++;
				}
				for (int i = 0; i < cmd.Parameter.Count - tailCandidates; i++)
					arguments[i] = AstToCommandResult(cmd.Parameter[i]);
				return new RootCommand(arguments);
			case AstType.Value:
				var astNode = (AstValue)node;
				// Quoted strings are always strings, the rest gets automatically converted
				if (astNode.StringType == StringType.FreeString)
					return new AutoConvertResultCommand(astNode.Value);
				else
					return new ResultCommand(new PrimitiveResult<string>(astNode.Value));
			default:
				throw Tools.UnhandledDefault(node.Type);
			}
		}

		public static object Execute(ExecutionInformation info, string command, IReadOnlyList<Type> returnTypes)
		{
			var ast = CommandParser.ParseCommandRequest(command);
			var cmd = AstToCommandResult(ast);
			return cmd.Execute(info, Array.Empty<ICommand>(), returnTypes);
		}

		public static object Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes)
			=> info.GetModule<CommandManager>().RootGroup.Execute(info, arguments, returnTypes);

		public static string ExecuteCommand(ExecutionInformation info, string command)
			=> CastResult(Execute(info, command, ReturnStringOrNothing));

		public static string ExecuteCommand(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
			=> CastResult(Execute(info, arguments, ReturnStringOrNothing));

		private static string CastResult(object result)
		{
			if (result is IPrimitiveResult<string> s)
				return s.Get();
			if (result == null)
				return null;
			throw new CommandException("Expected a string or nothing as result", CommandExceptionReason.NoReturnMatch);
		}

		public static object GetEmpty(IReadOnlyList<Type> resultTypes)
		{
			foreach (var item in resultTypes)
			{
				if (item == null)
					return null;
				else if (item == typeof(string))
					return string.Empty;
			}
			throw new CommandException("No empty return type available", CommandExceptionReason.NoReturnMatch);
		}

		public static string GetTree(ICommand com)
		{
			var strb = new TextModBuilder();
			GetTree(com, strb, 0);
			return strb.ToString();
		}

		private static void GetTree(ICommand com, TextModBuilder strb, int indent)
		{
			switch (com)
			{
			case CommandGroup group:
				strb.AppendFormat("<group>\n".Mod().Color(Color.Red));
				foreach (var subCom in group.Commands)
				{
					strb.Append(new string(' ', (indent + 1) * 2)).Append(subCom.Key);
					GetTree(subCom.Value, strb, indent + 1);
				}
				break;

			case FunctionCommand _:
				strb.AppendFormat("<func>\n".Mod().Color(Color.Green));
				break;

			case OverloadedFunctionCommand ofunc:
				strb.AppendFormat($"<overload({ofunc.Functions.Count})>\n".Mod().Color(Color.Blue));
				break;

			case AliasCommand _:
				strb.AppendFormat($"<alias>\n".Mod().Color(Color.Yellow));
				break;

			default:
				strb.AppendFormat("\n");
				break;
			}
		}

		#endregion
	}
}
