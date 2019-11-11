// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using TS3AudioBot.CommandSystem.Commands;
using TS3AudioBot.Localization;

namespace TS3AudioBot.CommandSystem
{
	[DebuggerDisplay("{DebuggerDisplay, nq}")]
	[JsonObject(MemberSerialization.OptIn)]
	public class BotCommand : FunctionCommand
	{
		private readonly string helpLookupName;
		private string cachedFullQualifiedName;

		[JsonProperty(PropertyName = "Name")]
		public string InvokeName { get; }
		private readonly string[] requiredRights;
		public string RequiredRight => requiredRights[0];
		[JsonProperty(PropertyName = "Description")]
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
					strb.Append(string.Join(", ", CommandParameter.Where(p => !p.Kind.IsNormal()).Select(p => p.Type.FullName).OrderBy(p => p)));
					strb.Append("|");
					strb.Append(string.Join(", ", CommandParameter.Where(p => p.Kind.IsNormal()).Select(p => p.Type.FullName)));
					strb.Append(")");
					cachedFullQualifiedName = strb.ToString();
				}
				return cachedFullQualifiedName;
			}
		}

		[JsonProperty(PropertyName = "Return")]
		public string Return { get; set; }
		[JsonProperty(PropertyName = "Parameter")]
		public (string name, string type, bool optional)[] Parameter { get; }
		[JsonProperty(PropertyName = "Modules")]
		public (string type, bool optional)[] Modules { get; }

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

		public BotCommand(CommandBuildInfo buildInfo) : base(buildInfo.Method, buildInfo.Parent)
		{
			InvokeName = buildInfo.CommandData.CommandNameSpace;
			helpLookupName = buildInfo.CommandData.OverrideHelpName ?? ("cmd_" + InvokeName.Replace(" ", "_") + "_help");
			requiredRights = new[] { "cmd." + string.Join(".", InvokeName.Split(' ')) };
			UsageList = buildInfo.UsageList?.ToArray() ?? Array.Empty<UsageAttribute>();
			// Serialization
			Return = UnwrapReturnType(CommandReturn).Name;
			Parameter = (
				from x in CommandParameter
				where x.Kind.IsNormal()
				select (x.Name, UnwrapParamType(x.Type).Name, x.Optional)).ToArray();
			Modules = (
				from x in CommandParameter
				where x.Kind == ParamKind.Dependency
				select (x.Type.Name, x.Optional)).ToArray();
		}

		public override string ToString()
		{
			var strb = new StringBuilder();
			strb.Append("\n!")
				.Append(InvokeName);

			foreach (var (name, _, optional) in Parameter)
			{
				strb.Append(' ');
				if (optional)
					strb.Append("[<").Append(name).Append(">]");
				else
					strb.Append('<').Append(name).Append('>');
			}

			strb.Append(": ")
				.Append(Description ?? strings.error_no_help ?? "<No help found>");

			if (UsageList.Length > 0)
			{
				int longest = UsageList.Max(p => p.UsageSyntax.Length) + 1;
				foreach (var para in UsageList)
					strb.Append("\n!").Append(InvokeName).Append(" ").Append(para.UsageSyntax)
						.Append(' ', longest - para.UsageSyntax.Length).Append(para.UsageHelp);
			}
			return strb.ToString();
		}

		public override object Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes)
		{
			// Check call complexity
			info.UseComplexityTokens(1);

			// Check permissions
			if (!info.HasRights(requiredRights))
				throw new CommandException(string.Format(strings.error_missing_right, InvokeName, RequiredRight), CommandExceptionReason.MissingRights);

			return base.Execute(info, arguments, returnTypes);
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
