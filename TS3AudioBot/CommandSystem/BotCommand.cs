namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using TS3Query;
	using static CommandRights;

	public class BotCommand : FunctionCommand
	{
		string cachedHelp = null;
		string cachedFullQualifiedName = null;

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
		public string FullQualifiedName
		{
			get
			{
				if (cachedFullQualifiedName == null)
				{
					var strb = new StringBuilder();
					strb.Append(InvokeName);
					strb.Append(" (");
					strb.Append(string.Join(", ", CommandParameter.Where(p => !SpecialTypes.Contains(p)).Select(p => p.FullName)));
					strb.Append(")");
					cachedFullQualifiedName = strb.ToString();
				}
				return cachedFullQualifiedName;
			}
		}

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
}
