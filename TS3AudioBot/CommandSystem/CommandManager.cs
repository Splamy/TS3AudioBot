namespace TS3AudioBot.CommandSystem
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text.RegularExpressions;
	using Helper;

	public class CommandManager : MarshalByRefObject
	{
		private HashSet<string> CommandPaths;
		public XCommandSystem CommandSystem { get; }

		public IList<BotCommand> BaseCommands { get; private set; }
		public IDictionary<Plugin, IList<BotCommand>> PluginCommands { get; }

		public IEnumerable<BotCommand> AllCommands
		{
			get
			{
				foreach (var com in BaseCommands)
					yield return com;
				foreach (var comArr in PluginCommands.Values)
					foreach (var com in comArr)
						yield return com;
				// todo alias
			}
		}

		private static readonly Regex CommandNamespaceValidator = new Regex(@"^[a-z]+( [a-z]+)*$", RegexOptions.Compiled);

		public CommandManager()
		{
			CommandSystem = new XCommandSystem();
			PluginCommands = new Dictionary<Plugin, IList<BotCommand>>();
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

		private void LoadCommand(BotCommand com) // TODO test
		{
			if (!CommandNamespaceValidator.IsMatch(com.InvokeName))
				throw new InvalidOperationException("BotCommand has an invalid invoke name: " + com.InvokeName);
			if (CommandPaths.Contains(com.FullQualifiedName))
				throw new InvalidOperationException("Command already exists: " + com.InvokeName);
			CommandPaths.Add(com.FullQualifiedName);

			var comPath = com.InvokeName.Split(' ');

			CommandGroup group = CommandSystem.RootCommand;
			// this for loop iterates through the seperate names of
			// the command to be added.
			for (int i = 0; i < comPath.Length - 1; i++)
			{
				ICommand currentCommand = group.GetCommand(comPath[i]);

				// if a group to hold the next level command doesn't exist
				// it will be created here
				if (currentCommand == null)
				{
					var nextGroup = new CommandGroup();
					group.AddCommand(comPath[i], nextGroup);
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
					group.RemoveCommand(comPath[i]);
					group.AddCommand(comPath[i], subGroup);

					subGroup.AddCommand(string.Empty, currentCommand);
					group = subGroup;
				}
				else
					throw new InvalidOperationException("An overloaded command cannot be replaced by a CommandGroup: " + com.InvokeName);
			}

			ICommand subCommand = group.GetCommand(comPath.Last());
			// the group we are trying to insert has no element with the current
			// name, so just insert it
			if (subCommand == null)
			{
				group.AddCommand(comPath.Last(), com);
				return;
			}
			// if we have a simple function, we need to create a overlaoder
			// and then add both functions to it
			else if (subCommand is FunctionCommand)
			{
				group.RemoveCommand(comPath.Last());
				var overloader = new OverloadedFunctionCommand();
				overloader.AddCommand((FunctionCommand)subCommand);
				overloader.AddCommand(com);
				group.AddCommand(comPath.Last(), overloader);
			}
			// if we have a overloaded function, we can simply add it
			else if (subCommand is OverloadedFunctionCommand)
			{
				var insertCommand = (OverloadedFunctionCommand)subCommand;
				insertCommand.AddCommand(com);
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
					if (com.NormalParameters > 0)
						Log.Write(Log.Level.Warning, "parameter of an empty named function under a group will be ignored!!");
				}
				else
					throw new InvalidOperationException("An empty named function under a group cannot be overloaded (" + com.InvokeName + ")");
			}
			else
				throw new InvalidOperationException("Unknown insertion error with " + com.FullQualifiedName);
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
