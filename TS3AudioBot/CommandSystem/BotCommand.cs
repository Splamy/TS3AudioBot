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
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using TS3Client;

	public class BotCommand : FunctionCommand
	{
		private string cachedHelp = null;
		private string cachedFullQualifiedName = null;

		public BotCommand(CommandBuildInfo buildInfo) : base(buildInfo.method, buildInfo.parent, buildInfo.reqiredParameters?.Count)
		{
			InvokeName = buildInfo.commandData.CommandNameSpace;
			requiredRights = new string[] { "cmd." + string.Join(".", InvokeName.Split(' ')) };
			Description = buildInfo.commandData.CommandHelp;
			UsageList = buildInfo.usageList ?? Enumerable.Empty<UsageAttribute>();
		}

		public string InvokeName { get; }
		private string[] requiredRights;
		public string RequiredRight => requiredRights[0];
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
				var strb = new StringBuilder();
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
			strb.Append(" : ");
			foreach (var param in UsageList)
				strb.Append(param.UsageSyntax).Append('/');
			return strb.ToString();
		}

		public override ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (!info.Session.HasRights(requiredRights))
				throw new CommandException($"You cannot execute \"{InvokeName}\". You are missing the \"{RequiredRight}\" right.!",
					CommandExceptionReason.MissingRights);
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
