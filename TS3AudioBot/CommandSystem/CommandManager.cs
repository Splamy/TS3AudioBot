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
		private HashSet<string> CommandPaths;
		public XCommandSystem CommandSystem { get; }

		public IList<BotCommand> BaseCommands { get; private set; }
		private List<BotCommand> dynamicCommands;
		public IDictionary<Plugin, IList<BotCommand>> PluginCommands { get; }

		public IEnumerable<BotCommand> AllCommands
		{
			get
			{
				foreach (var com in BaseCommands)
					yield return com;
				foreach (var com in dynamicCommands)
					yield return com;
				foreach (var comArr in PluginCommands.Values)
					foreach (var com in comArr)
						yield return com;
				// todo alias
			}
		}

		private static readonly Regex CommandNamespaceValidator = new Regex(@"^[a-z]+( [a-z]+)*$", Util.DefaultRegexConfig & ~RegexOptions.IgnoreCase);

		public CommandManager()
		{
			CommandSystem = new XCommandSystem();
			PluginCommands = new Dictionary<Plugin, IList<BotCommand>>();
			Util.Init(ref dynamicCommands);
			Util.Init(ref CommandPaths);
		}

		public void RegisterMain(MainBot main)
		{
			if (BaseCommands != null)
				throw new InvalidOperationException("Operation can only be executed once.");

			var comList = new List<BotCommand>();
			foreach (var com in GetBotCommands(GetCommandMethods(main)))
			{
				LoadCommand(com);
				comList.Add(com);
			}

			BaseCommands = comList.AsReadOnly();
		}

		public void RegisterCommand(BotCommand command)
		{
			LoadCommand(command);
			dynamicCommands.Add(command);
		}

		internal void RegisterCommand(ICommand command, string path)
		{
			LoadICommand(command, path);
			// TODO: add BotCommand (tree-)scan
		}

		public void RegisterPlugin(Plugin plugin)
		{
			if (PluginCommands.ContainsKey(plugin))
				throw new InvalidOperationException("Plugin is already laoded.");

			var comList = plugin.GetWrappedCommands().ToList();

			CheckDistinct(comList);

			PluginCommands.Add(plugin, comList.AsReadOnly());

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

		public void UnregisterPlugin(Plugin plugin)
		{
			IList<BotCommand> commands;
			if (PluginCommands.TryGetValue(plugin, out commands))
			{
				foreach (var com in commands)
				{
					UnloadCommand(com);
				}
			}
		}

		private static IEnumerable<BotCommand> GetBotCommands(IEnumerable<CommandBuildInfo> methods)
		{
			foreach (var botData in methods)
			{
				botData.usageList = botData.method.GetCustomAttributes<UsageAttribute>();
				yield return new BotCommand(botData);
			}
		}

		public static IEnumerable<CommandBuildInfo> GetCommandMethods(object obj)
		{
			var objType = obj.GetType();
			foreach (var method in objType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
			{
				var comAtt = method.GetCustomAttribute<CommandAttribute>();
				if (comAtt == null) continue;
				var reqAtt = method.GetCustomAttribute<RequiredParametersAttribute>();
				yield return new CommandBuildInfo(obj, method, comAtt, reqAtt);
			}
		}

		private void CheckDistinct(IList<BotCommand> list) // TODO test
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
			if (CommandPaths.Contains(com.FullQualifiedName))
				throw new InvalidOperationException("Command already exists: " + com.InvokeName);
			CommandPaths.Add(com.FullQualifiedName);

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

		private R InsertInto(CommandGroup group, ICommand com, string name)
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
			else if (subCommand is CommandGroup)
			{
				var insertCommand = (CommandGroup)subCommand;
				var noparamCommand = insertCommand.GetCommand(string.Empty);

				if (noparamCommand == null)
				{
					insertCommand.AddCommand(string.Empty, com);
					var botCom = com as BotCommand;
					if (botCom != null && botCom.NormalParameters > 0)
						Log.Write(Log.Level.Warning, $"\"{botCom.FullQualifiedName}\" has at least one parameter and won't be reachable due to an overloading function.");
					return R.OkR;
				}
				else
					return "An empty named function under a group cannot be overloaded.";
			}

			FunctionCommand funcCom = com as FunctionCommand;
			if (funcCom == null)
				return $"The command cannot be inserted into a complex node ({name}).";

			// if we have is a simple function, we need to create a overlaoder
			// and then add both functions to it
			if (subCommand is FunctionCommand)
			{
				group.RemoveCommand(name);
				var overloader = new OverloadedFunctionCommand();
				overloader.AddCommand((FunctionCommand)subCommand);
				overloader.AddCommand(funcCom);
				group.AddCommand(name, overloader);
			}
			// if we have a overloaded function, we can simply add it
			else if (subCommand is OverloadedFunctionCommand)
			{
				var insertCommand = (OverloadedFunctionCommand)subCommand;
				insertCommand.AddCommand(funcCom);
			}
			else
				return "Unknown node to insert to.";

			return R.OkR;
		}

		private void GenerateError(string msg, BotCommand involvedCom)
		{
			throw new InvalidOperationException(
					$@"Command error path: {involvedCom?.InvokeName}
					Command: {involvedCom?.FullQualifiedName}
					Error: {msg}");
		}

		private void UnloadCommand(BotCommand com)
		{
			if (!CommandPaths.Contains(com.FullQualifiedName))
				return;
			CommandPaths.Remove(com.FullQualifiedName);

			var comPath = com.InvokeName.Split(' ');

			CommandUnloadNode node = new CommandUnloadNode
			{
				parentNode = null,
				self = CommandSystem.RootCommand,
			};

			// build up the list to our desired node
			for (int i = 0; i < comPath.Length - 1; i++)
			{
				var nextGroup = node.self.GetCommand(comPath[i]) as CommandGroup;
				if (nextGroup == null)
					break;

				node = new CommandUnloadNode
				{
					parentNode = node,
					self = nextGroup,
				};
			}
			var subGroup = node.self.GetCommand(comPath.Last());
			// nothing to remove
			if (subGroup == null)
				return;
			// if the subnode is a plain FunctionCommand then we found our command to delete
			else if (subGroup is FunctionCommand)
			{
				node.self.RemoveCommand(com);
			}
			// here we can delete our command from the overloader
			else if (subGroup is OverloadedFunctionCommand)
			{
				((OverloadedFunctionCommand)subGroup).RemoveCommand(com);
			}
			// now to the special case when a command gets inserted with an empty string
			else if (subGroup is CommandGroup)
			{
				var insertGroup = (CommandGroup)subGroup;
				// since we check precisely that only one command and only a simple FunctionCommand
				// can be added with an empty string, wen can delte it safely this way
				insertGroup.RemoveCommand(string.Empty);
				// add the node for cleanup
				node = new CommandUnloadNode
				{
					parentNode = node,
					self = insertGroup,
				};
			}

			// and finally clean all empty nodes up
			while (node != null)
			{
				if (node.self.IsEmpty && node.parentNode != null)
					node.parentNode.self.RemoveCommand(node.self);
				node = node.parentNode;
			}
		}

		class CommandUnloadNode
		{
			public CommandUnloadNode parentNode;
			public CommandGroup self;
		}
	}
}
