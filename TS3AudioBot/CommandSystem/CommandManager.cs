namespace TS3AudioBot.CommandSystem
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using TS3Query;
	using Helper;
	using static CommandRights;

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

	public class CommandBuildInfo
	{
		public object parent;
		public MethodInfo method;
		public CommandAttribute commandData;
		public RequiredParametersAttribute reqiredParameters;
		public IEnumerable<UsageAttribute> usageList;

		public CommandBuildInfo(object p, MethodInfo m, CommandAttribute comAtt, RequiredParametersAttribute reqAtt)
		{
			parent = p;
			method = m;
			commandData = comAtt;
			reqiredParameters = reqAtt;
		}
	}

	public class BotCommand : FunctionCommand
	{
		string cachedHelp = null;

		public BotCommand(CommandBuildInfo buildInfo) : base(buildInfo.method, buildInfo.parent, buildInfo.reqiredParameters?.Count)
		{
			InvokeName = buildInfo.commandData.CommandNameSpace;
			CommandRights = buildInfo.commandData.RequiredRights;
			Description = buildInfo.commandData.CommandHelp;
			UsageList = buildInfo.usageList;
		}

		public string InvokeName { get; }
		public CommandRights CommandRights { get; }
		public string Description { get; private set; }
		public IEnumerable<UsageAttribute> UsageList { get; }

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
}
