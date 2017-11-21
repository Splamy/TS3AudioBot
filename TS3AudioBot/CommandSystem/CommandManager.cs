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
	using Helper;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text.RegularExpressions;

	public class CommandManager
	{
		private static readonly Regex CommandNamespaceValidator =
			new Regex(@"^[a-z]+( [a-z]+)*$", Util.DefaultRegexConfig & ~RegexOptions.IgnoreCase);

		private readonly List<BotCommand> baseCommands;
		private readonly HashSet<string> commandPaths;
		private readonly List<BotCommand> dynamicCommands;
		private readonly Dictionary<ICommandBag, IReadOnlyList<BotCommand>> pluginCommands;

		public CommandManager()
		{
			CommandSystem = new XCommandSystem();
			Util.Init(ref baseCommands);
			Util.Init(ref commandPaths);
			Util.Init(ref dynamicCommands);
			Util.Init(ref pluginCommands);
		}

		public XCommandSystem CommandSystem { get; }

		public IEnumerable<BotCommand> AllCommands
		{
			get
			{
				foreach (var com in baseCommands)
					yield return com;
				foreach (var com in dynamicCommands)
					yield return com;
				foreach (var comArr in pluginCommands.Values)
					foreach (var com in comArr)
						yield return com;
				// todo alias
			}
		}

		public IEnumerable<string> AllRights => AllCommands.Select(x => x.RequiredRight);

		public void RegisterMain()
		{
			if (baseCommands.Count > 0)
				throw new InvalidOperationException("Operation can only be executed once.");

			foreach (var com in GetBotCommands(GetCommandMethods(null, typeof(Commands))))
			{
				LoadCommand(com);
				baseCommands.Add(com);
			}
		}

		public void RegisterCommand(BotCommand command)
		{
			LoadCommand(command);
			dynamicCommands.Add(command);
		}

		internal void RegisterCollection(ICommandBag bag)
		{
			if (pluginCommands.ContainsKey(bag))
				throw new InvalidOperationException("This bag is already laoded.");

			var comList = bag.ExposedCommands.ToList();

			CheckDistinct(comList);

			pluginCommands.Add(bag, comList.AsReadOnly());

			int loaded = 0;
			try
			{
				for (; loaded < comList.Count; loaded++)
					LoadCommand(comList[loaded]);
			}
			catch // TODO test
			{
				for (int i = 0; i <= loaded && i < comList.Count; i++)
					UnloadCommand(comList[i]);
				throw;
			}
		}

		internal void UnregisterCollection(ICommandBag bag)
		{
			if (pluginCommands.TryGetValue(bag, out var commands))
			{
				pluginCommands.Remove(bag);
				foreach (var com in commands)
				{
					UnloadCommand(com);
				}
			}
		}

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
			if (obj == null && type == null)
				throw new ArgumentNullException(nameof(type), "No type information given.");
			var objType = type ?? obj.GetType();

			foreach (var method in objType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
			{
				var comAtt = method.GetCustomAttribute<CommandAttribute>();
				if (comAtt == null) continue;
				var reqAtt = method.GetCustomAttribute<RequiredParametersAttribute>();
				yield return new CommandBuildInfo(obj, method, comAtt, reqAtt);
			}
		}

		private static void CheckDistinct(ICollection<BotCommand> list) // TODO test
		{
			if (list.Select(c => c.InvokeName).Distinct().Count() < list.Count)
			{
				var duplicates = list.GroupBy(c => c.InvokeName).Where(g => g.Count() > 1).Select(g => g.Key);
				throw new InvalidOperationException("The object contains duplicates: " + string.Join(", ", duplicates));
			}
		}

		// TODO: prevent stupid behaviour like:
		// string A(int b)
		// string A(ExecutionInformation i, int b)
		// since the CommandManager can't distinguish these two, when calling
		private void LoadCommand(BotCommand com) // TODO test
		{
			if (!CommandNamespaceValidator.IsMatch(com.InvokeName))
				throw new InvalidOperationException("BotCommand has an invalid invoke name: " + com.InvokeName);
			if (commandPaths.Contains(com.FullQualifiedName))
				throw new InvalidOperationException("Command already exists: " + com.InvokeName);
			commandPaths.Add(com.FullQualifiedName);

			LoadICommand(com, com.InvokeName);
		}

		private void LoadICommand(ICommand com, string path)
		{
			string[] comPath = path.Split(' ');

			var buildResult = BuildAndGet(comPath.Take(comPath.Length - 1));
			if (!buildResult)
				GenerateError(buildResult.Message, com as BotCommand);

			var result = InsertInto(buildResult.Value, com, comPath.Last());
			if (!result)
				GenerateError(result.Message, com as BotCommand);
		}

		private R<CommandGroup> BuildAndGet(IEnumerable<string> comPath)
		{
			CommandGroup group = CommandSystem.RootCommand;
			// this for loop iterates through the seperate names of
			// the command to be added.
			foreach (var comPathPart in comPath)
			{
				ICommand currentCommand = group.GetCommand(comPathPart);

				// if a group to hold the next level command doesn't exist
				// it will be created here
				if (currentCommand == null)
				{
					var nextGroup = new CommandGroup();
					group.AddCommand(comPathPart, nextGroup);
					group = nextGroup;
				}
				// if the group already exists we can take it.
				else if (currentCommand is CommandGroup)
				{
					group = (CommandGroup)currentCommand;
				}
				// if the element is anything else, we have to replace it
				// with a group and put the old element back into it.
				else if (currentCommand is FunctionCommand)
				{
					var subGroup = new CommandGroup();
					group.RemoveCommand(comPathPart);
					group.AddCommand(comPathPart, subGroup);
					if (!InsertInto(group, currentCommand, comPathPart))
						throw new InvalidOperationException("Unexpected group error");
					group = subGroup;
				}
				else
					return "An overloaded command cannot be replaced by a CommandGroup";
			}

			return group;
		}

		private static R InsertInto(CommandGroup group, ICommand com, string name)
		{
			ICommand subCommand = group.GetCommand(name);
			// the group we are trying to insert has no element with the current
			// name, so just insert it
			if (subCommand == null)
			{
				group.AddCommand(name, com);
				return R.OkR;
			}
			// to add a command to CommandGroup will have to treat it as a subcommand
			// with an empty string as a name
			else if (subCommand is CommandGroup insertCommand)
			{
				var noparamCommand = insertCommand.GetCommand(string.Empty);

				if (noparamCommand == null)
				{
					insertCommand.AddCommand(string.Empty, com);
					if (com is BotCommand botCom && botCom.NormalParameters > 0)
						Log.Write(Log.Level.Warning, $"\"{botCom.FullQualifiedName}\" has at least one parameter and won't be reachable due to an overloading function.");
					return R.OkR;
				}
				else
					return "An empty named function under a group cannot be overloaded.";
			}

			if (!(com is FunctionCommand funcCom))
				return $"The command cannot be inserted into a complex node ({name}).";

			// if we have is a simple function, we need to create a overlaoder
			// and then add both functions to it
			if (subCommand is FunctionCommand subFuncCommand)
			{
				group.RemoveCommand(name);
				var overloader = new OverloadedFunctionCommand();
				overloader.AddCommand(subFuncCommand);
				overloader.AddCommand(funcCom);
				group.AddCommand(name, overloader);
			}
			// if we have a overloaded function, we can simply add it
			else if (subCommand is OverloadedFunctionCommand insertCommand)
			{
				insertCommand.AddCommand(funcCom);
			}
			else
				return "Unknown node to insert to.";

			return R.OkR;
		}

		private static void GenerateError(string msg, BotCommand involvedCom)
		{
			throw new InvalidOperationException(
				$@"Command error path: {involvedCom?.InvokeName}
					Command: {involvedCom?.FullQualifiedName}
					Error: {msg}");
		}

		private void UnloadCommand(BotCommand com)
		{
			if (!commandPaths.Contains(com.FullQualifiedName))
				return;
			commandPaths.Remove(com.FullQualifiedName);

			var comPath = com.InvokeName.Split(' ');

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
			if (subGroup == null)
				return;
			// if the subnode is a plain FunctionCommand then we found our command to delete
			else if (subGroup is FunctionCommand)
			{
				node.Self.RemoveCommand(com);
			}
			// here we can delete our command from the overloader
			else if (subGroup is OverloadedFunctionCommand subOverloadGroup)
			{
				subOverloadGroup.RemoveCommand(com);
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
				if (node.Self.IsEmpty && node.ParentNode != null)
					node.ParentNode.Self.RemoveCommand(node.Self);
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
