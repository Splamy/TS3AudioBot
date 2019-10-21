// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem
{
	using Commands;
	using Helper;
	using Rights;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text.RegularExpressions;
	using TS3AudioBot.Localization;

	/// <summary>Mangement for the bot command system.</summary>
	public class CommandManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Regex CommandNamespaceValidator =
			new Regex(@"^[a-z\d]+( [a-z\d]+)*$", Util.DefaultRegexConfig & ~RegexOptions.IgnoreCase);

		private readonly Dictionary<string, AliasCommand> aliasPaths;
		private readonly HashSet<string> commandPaths;
		private readonly HashSet<ICommandBag> baggedCommands;
		private readonly RightsManager rightsManager;

		public CommandManager(RightsManager rightsManager)
		{
			CommandSystem = new XCommandSystem();
			Util.Init(out aliasPaths);
			Util.Init(out commandPaths);
			Util.Init(out baggedCommands);
			this.rightsManager = rightsManager;
		}

		public XCommandSystem CommandSystem { get; }

		public IEnumerable<BotCommand> AllCommands => baggedCommands.SelectMany(x => x.BagCommands);

		public IEnumerable<string> AllRights => AllCommands.Select(x => x.RequiredRight).Concat(baggedCommands.SelectMany(x => x.AdditionalRights));

		public void RegisterCollection(ICommandBag bag)
		{
			if (baggedCommands.Contains(bag))
				throw new InvalidOperationException("This bag is already loaded.");

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

			var dac = new AliasCommand(CommandSystem, command);
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
			CommandGroup group = CommandSystem.RootCommand;
			// this for loop iterates through the separate names of
			// the command to be added.
			foreach (var comPathPart in comPath)
			{
				ICommand currentCommand = group.GetCommand(comPathPart);

				// if a group to hold the next level command doesn't exist
				// it will be created here
				if (currentCommand is null)
				{
					var nextGroup = new CommandGroup();
					group.AddCommand(comPathPart, nextGroup);
					group = nextGroup;
				}
				// if the group already exists we can take it.
				else if (currentCommand is CommandGroup cgCommand)
				{
					group = cgCommand;
				}
				// if the element is anything else, we have to replace it
				// with a group and put the old element back into it.
				else if (currentCommand is FunctionCommand)
				{
					var subGroup = new CommandGroup();
					group.RemoveCommand(comPathPart);
					group.AddCommand(comPathPart, subGroup);
					var insertResult = InsertInto(group, currentCommand, comPathPart);
					if (!insertResult.Ok)
						return insertResult.Error;
					group = subGroup;
				}
				else
				{
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
				Self = CommandSystem.RootCommand,
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
			// nothing to remove
			if (subGroup is null)
				return;
			// if the subnode is a plain FunctionCommand then we found our command to delete
			else if (subGroup is FunctionCommand || subGroup is AliasCommand)
			{
				node.Self.RemoveCommand(com);
			}
			// here we can delete our command from the overloader
			else if (subGroup is OverloadedFunctionCommand subOverloadGroup)
			{
				if (com is FunctionCommand funcCom)
					subOverloadGroup.RemoveCommand(funcCom);
				else
					return;
			}
			// now to the special case when a command gets inserted with an empty string
			else if (subGroup is CommandGroup insertGroup)
			{
				// since we check precisely that only one command and only a simple FunctionCommand
				// can be added with an empty string, wen can delete it safely this way
				insertGroup.RemoveCommand(string.Empty);
				// add the node for cleanup
				node = new CommandUnloadNode
				{
					ParentNode = node,
					Self = insertGroup,
				};
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
	}
}
