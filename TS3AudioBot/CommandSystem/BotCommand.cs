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
	using CommandResults;
	using Commands;
	using Localization;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Reflection;
	using System.Text;

	[DebuggerDisplay("{DebuggerDisplay, nq}")]
	public class BotCommand : FunctionCommand
	{
		private readonly string helpLookupName;
		private string cachedFullQualifiedName;
		private object cachedAsJsonObj;

		public string InvokeName { get; }
		private readonly string[] requiredRights;
		public string RequiredRight => requiredRights[0];
		public string Description => LocalizationManager.GetString(helpLookupName);
		public UsageAttribute[] UsageList { get; }
		public string FullQualifiedName
		{
			get
			{
				if (cachedFullQualifiedName is null)
				{
					var strb = new StringBuilder();
					strb.Append(InvokeName);
					strb.Append(" (");
					strb.Append(string.Join(", ", CommandParameter.Where(p => p.kind.IsNormal()).Select(p => p.type.FullName)));
					strb.Append(")");
					cachedFullQualifiedName = strb.ToString();
				}
				return cachedFullQualifiedName;
			}
		}

		public string DebuggerDisplay
		{
			get
			{
				var strb = new StringBuilder();
				strb.Append('!').Append(InvokeName);
				strb.Append(" : ");
				foreach (var param in UsageList)
					strb.Append(param.UsageSyntax).Append('/');
				return strb.ToString();
			}
		}

		public object AsJsonObj
		{
			get
			{
				if (cachedAsJsonObj is null)
					cachedAsJsonObj = new CommadSerializeObj(this);
				return cachedAsJsonObj;
			}
		}

		public BotCommand(CommandBuildInfo buildInfo) : base(buildInfo.Method, buildInfo.Parent)
		{
			InvokeName = buildInfo.CommandData.CommandNameSpace;
			helpLookupName = buildInfo.CommandData.OverrideHelpName ?? ("cmd_" + InvokeName.Replace(" ", "_") + "_help");
			requiredRights = new[] { "cmd." + string.Join(".", InvokeName.Split(' ')) };
			UsageList = buildInfo.UsageList?.ToArray() ?? Array.Empty<UsageAttribute>();
		}

		public override string ToString()
		{
			var strb = new StringBuilder();
			strb.Append("\n!").Append(InvokeName).Append(": ").Append(Description ?? strings.error_no_help ?? "<No help found>");

			if (UsageList.Length > 0)
			{
				int longest = UsageList.Max(p => p.UsageSyntax.Length) + 1;
				foreach (var para in UsageList)
					strb.Append("\n!").Append(InvokeName).Append(" ").Append(para.UsageSyntax)
						.Append(' ', longest - para.UsageSyntax.Length).Append(para.UsageHelp);
			}
			return strb.ToString();
		}

		public override ICommandResult Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			if (!info.HasRights(requiredRights))
				throw new CommandException(string.Format(strings.error_missing_right, InvokeName, RequiredRight),
					CommandExceptionReason.MissingRights);

			return base.Execute(info, arguments, returnTypes);
		}

		private class CommadSerializeObj
		{
			private readonly BotCommand botCmd;
			public string Name => botCmd.InvokeName;
			public string Description => botCmd.Description;
			public string[] Parameter { get; }
			public string[] Modules { get; }
			public string Return { get; }

			public CommadSerializeObj(BotCommand botCmd)
			{
				this.botCmd = botCmd;
				Parameter = (
					from x in botCmd.CommandParameter
					where x.kind.IsNormal()
					select UnwrapParamType(x.type).Name + (x.optional ? "?" : "")).ToArray();
				Modules = (
					from x in botCmd.CommandParameter
					where x.kind == ParamKind.Dependency
					select x.type.Name + (x.optional ? "?" : "")).ToArray();
				Return = UnwrapReturnType(botCmd.CommandReturn).Name;
			}

			public override string ToString() => botCmd.ToString();
		}
	}

	public class CommandBuildInfo
	{
		public object Parent { get; }
		public MethodInfo Method { get; }
		public CommandAttribute CommandData { get; }
		public UsageAttribute[] UsageList { get; set; }

		public CommandBuildInfo(object p, MethodInfo m, CommandAttribute comAtt)
		{
			Parent = p;
			Method = m;
			if (!m.IsStatic && p is null)
				throw new ArgumentException("Got instance method without accociated object");
			CommandData = comAtt;
		}
	}
}
