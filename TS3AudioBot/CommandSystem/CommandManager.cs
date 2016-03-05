namespace TS3AudioBot.CommandSystem
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using TS3Query;
	using static CommandRights;

	public class CommandManager
	{
		private HashSet<string> CommandPaths;
		public XCommandSystem CommandSystem { get; }

		public IList<BotCommand> BaseCommands { get; private set; }
		public Dictionary<ITS3ABPlugin, IList<BotCommand>> PluginCommands { get; }

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

		public CommandManager()
		{
			CommandSystem = new XCommandSystem();
			PluginCommands = new Dictionary<ITS3ABPlugin, IList<BotCommand>>();
			CommandPaths = new HashSet<string>();
		}

		public void RegisterMain(MainBot main)
		{
			if (BaseCommands != null)
				throw new InvalidOperationException("Operation can only be executed once.");

			var comList = new List<BotCommand>();
			foreach (var com in GetCommandMethods(main, CommandType.Main))
			{
				LoadCommand(com);
				comList.Add(com);
			}

			BaseCommands = comList.AsReadOnly();
		}

		public void RegisterPlugin(ITS3ABPlugin plugin)
		{
			if (PluginCommands.ContainsKey(plugin))
				throw new InvalidOperationException("Plugin is already laoded.");

			var comList = GetCommandMethods(plugin, CommandType.Plugin).ToList();

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

		private IEnumerable<BotCommand> GetCommandMethods(object obj, CommandType type)
		{
			var objType = obj.GetType();
			foreach (var method in objType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
			{
				var comAtt = method.GetCustomAttribute<CommandAttribute>();
				if (comAtt == null) continue;

				var reqAtt = method.GetCustomAttribute<RequiredParametersAttribute>();

				var botCommand = new BotCommand
					(comAtt.CommandNameSpace,
					comAtt.RequiredRights,
					comAtt.CommandHelp,
					method.GetCustomAttributes<UsageAttribute>(),
					method,
					method.IsStatic ? null : obj,
					reqAtt == null ? null : (int?)reqAtt.Count);
				botCommand.CommandType = type;

				yield return botCommand;
			}
			yield break;
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
			if (CommandPaths.Contains(com.InvokeName))
				throw new InvalidOperationException("Command already exists: " + com.InvokeName);
			CommandPaths.Add(com.InvokeName);

			var comPath = com.InvokeName.Split(' ');
			
			CommandGroup group = CommandSystem.RootCommand;
			for (int i = 0; i < comPath.Length - 1; i++)
			{
				ICommand curCom = group.GetCommand(comPath[i]);
				if (curCom == null)
				{
					var nextGroup = new CommandGroup();
					group.AddCommand(comPath[i], nextGroup);
					group = nextGroup;
				}
				else if ((group = curCom as CommandGroup) == null)
					throw new InvalidOperationException("The requested command cannot be extended.");
			}

			if (group == null)
				throw new InvalidOperationException("No group found to add to.");
			group.AddCommand(comPath.Last(), com);
		}

		private void UnloadCommand(BotCommand com)
		{
			if (!CommandPaths.Contains(com.InvokeName))
				return;
			CommandPaths.Remove(com.InvokeName);

			var comPath = com.InvokeName.Split(' ');

			var comPathStack = new Stack<Tuple<CommandGroup, CommandGroup, string>>();
			CommandGroup group = CommandSystem.RootCommand;
			for (int i = 0; i < comPath.Length - 1; i++)
			{
				var nextGroup = group.GetCommand(comPath[i]) as CommandGroup;
				if (group == null)
					break;
				comPathStack.Push(new Tuple<CommandGroup, CommandGroup, string>(group, nextGroup, comPath[i]));
				group = nextGroup;
			}
			if (group != null && group.ContainsCommand(comPath.Last()))
				group.RemoveCommand(comPath.Last());
			while (comPathStack.Any())
			{
				var curGroup = comPathStack.Pop();
				if (curGroup.Item2.IsEmpty && curGroup.Item1.ContainsCommand(curGroup.Item3))
					curGroup.Item1.RemoveCommand(curGroup.Item3);
			}
		}
	}

	public class BotCommand : FunctionCommand
	{
		string cachedHelp = null;

		public BotCommand(string invokeName, CommandRights rights, string descripion, IEnumerable<UsageAttribute> usageList,
				MethodInfo command, object parentObject, int? requiredParameters)
			: base(command, parentObject, requiredParameters)
		{
			InvokeName = invokeName;
			CommandRights = rights;
			Description = descripion;
			UsageList = usageList;
		}

		public string InvokeName { get; }
		public CommandRights CommandRights { get; }
		public string Description { get; private set; }
		public IEnumerable<UsageAttribute> UsageList { get; }
		internal CommandType CommandType { get; set; }

		public string GetHelp()
		{
			if (cachedHelp == null)
			{
				StringBuilder strb = new StringBuilder();
				if (!string.IsNullOrEmpty(Description))
					strb.Append("\n!").Append(InvokeName).Append(": ").Append(Description);

				if (UsageList.Any())
				{
					int longest = UsageList.Max(p => p.UsageSyntax.Length) + 1;
					foreach (var para in UsageList)
						strb.Append("\n!").Append(InvokeName).Append(" ").Append(para.UsageSyntax)
							.Append(' ', longest - para.UsageSyntax.Length).Append(para.UsageHelp);
				}
				cachedHelp = strb.ToString();
			}
			return cachedHelp;
		}

		public override string ToString()
		{
			var strb = new StringBuilder();
			strb.Append('!').Append(InvokeName);
			strb.Append(" - ").Append(CommandRights);
			strb.Append(" : ");
			foreach (var param in UsageList)
				strb.Append(param.UsageSyntax).Append('/');
			return strb.ToString();
		}

		public override ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (info.IsAdmin.Value)
				return base.Execute(info, arguments, returnTypes);

			switch (CommandRights)
			{
			case Admin:
				throw new CommandException("Command must be invoked by an admin!");
			case Public:
				if (info.TextMessage.Target != MessageTarget.Server)
					throw new CommandException("Command must be used in public mode!");
				break;
			case Private:
				if (info.TextMessage.Target != MessageTarget.Private)
					throw new CommandException("Command must be used in a private session!");
				break;
			}
			return base.Execute(info, arguments, returnTypes);
		}
	}

	internal enum CommandType
	{
		Main,
		Plugin,
		Alias,
	}
}
