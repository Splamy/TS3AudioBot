// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Nett;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using TS3AudioBot.Helper;
using TS3AudioBot.Rights.Matchers;
using TSLib;

namespace TS3AudioBot.Rights
{
	// Adding a new Matcher:
	// 1) Add public MatchHashSet
	// 2) Add To Has Matches condition when empty
	// 3) Add To FillNull when not declared
	// 4) Add new case to ParseKey switch
	// 5) Add Property in the ExecuteContext class
	// 6) Add match condition to RightManager.ProcessNode
	// 7) Set value in RightManager.GetRightsContext

	internal class RightsRule : RightsDecl
	{
		public List<RightsDecl> Children { get; set; }
		public IEnumerable<RightsRule> ChildrenRules => Children.OfType<RightsRule>();
		public IEnumerable<RightsGroup> ChildrenGroups => Children.OfType<RightsGroup>();

		public List<Matcher> Matcher { get; }

		public RightsRule()
		{
			Children = new List<RightsDecl>();
			Matcher = new List<Matcher>();
		}

		public bool HasMatcher() => Matcher.Count > 0;

		public bool Matches(ExecuteContext ctx)
		{
			if (!HasMatcher())
				return true;

			foreach (var matcher in Matcher)
			{
				if (matcher.Matches(ctx))
					return true;
			}

			return false;
		}

		public override bool ParseKey(string key, TomlObject tomlObj, ParseContext ctx)
		{
			if (base.ParseKey(key, tomlObj, ctx))
				return true;

			switch (key)
			{
			case "host":
				if (tomlObj.TryGetValueArray<string>(out var host)) Matcher.Add(new MatchHost(host));
				else ctx.Errors.Add("<host> Field has invalid data.");
				return true;
			case "groupid":
				if (tomlObj.TryGetValueArray<ulong>(out var servergroupid)) Matcher.Add(new MatchServerGroupId(servergroupid.Select(ServerGroupId.To)));
				else ctx.Errors.Add("<groupid> Field has invalid data.");
				return true;
			case "channelgroupid":
				if (tomlObj.TryGetValueArray<ulong>(out var channelgroupid)) Matcher.Add(new MatchChannelGroupId(channelgroupid.Select(ChannelGroupId.To)));
				else ctx.Errors.Add("<channelgroupid> Field has invalid data.");
				return true;
			case "useruid":
				if (tomlObj.TryGetValueArray<string>(out var useruid)) Matcher.Add(new MatchClientUid(useruid.Select(Uid.To)));
				else ctx.Errors.Add("<useruid> Field has invalid data.");
				return true;
			case "perm":
				if (tomlObj.TryGetValueArray<string>(out var perm)) Matcher.Add(new MatchPermission(perm, ctx));
				else ctx.Errors.Add("<perm> Field has invalid data.");
				return true;
			case "apitoken":
				if (tomlObj.TryGetValueArray<string>(out var apitoken)) Matcher.Add(new MatchToken(apitoken));
				else ctx.Errors.Add("<apitoken> Field has invalid data.");
				return true;
			case "bot":
				if (tomlObj.TryGetValueArray<string>(out var bot)) Matcher.Add(new MatchBot(bot));
				else ctx.Errors.Add("<bot> Field has invalid data.");
				return true;
			case "isapi":
				if (tomlObj.TryGetValue<bool>(out var isapi)) Matcher.Add(new MatchIsApi(isapi));
				else ctx.Errors.Add("<isapi> Field has invalid data.");
				return true;
			case "ip":
				if (tomlObj.TryGetValueArray<string>(out var ip))
				{
					Matcher.Add(new MatchApiCallerIp(ip.Select(x =>
					{
						if (IPAddress.TryParse(x, out var ipa))
							return ipa;
						ctx.Errors.Add($"<ip> Field value '{x}' could not be parsed.");
						return null!;
					}).Where(x => x != null)));
				}
				else ctx.Errors.Add("<ip> Field has invalid data.");
				return true;
			case "visibility":
				if (tomlObj.TryGetValueArray<TextMessageTargetMode>(out var visibility)) Matcher.Add(new MatchVisibility(visibility));
				else ctx.Errors.Add("<visibility> Field has invalid data.");
				return true;
			case "rule":
				if (tomlObj.TomlType == TomlObjectType.ArrayOfTables)
				{
					var childTables = (TomlTableArray)tomlObj;
					foreach (var childTable in childTables.Items)
					{
						var rule = new RightsRule();
						Children.Add(rule);
						rule.Parent = this;
						rule.ParseChilden(childTable, ctx);
					}
					return true;
				}
				else
				{
					ctx.Errors.Add("Misused key with reserved name \"rule\".");
					return false;
				}
			default:
				// group
				if (key.StartsWith("$"))
				{
					if (tomlObj.TomlType == TomlObjectType.Table)
					{
						var childTable = (TomlTable)tomlObj;
						var group = new RightsGroup(key);
						Children.Add(group);
						group.Parent = this;
						group.ParseChilden(childTable, ctx);
						return true;
					}
					else
					{
						ctx.Errors.Add($"Misused key for group declaration: {key}.");
					}
				}
				return false;
			}
		}

		public override RightsGroup? ResolveGroup(string groupName, ParseContext ctx)
		{
			foreach (var child in ChildrenGroups)
			{
				if (child.Name == groupName)
					return child;
			}
			return Parent?.ResolveGroup(groupName, ctx);
		}

		public override string ToString()
		{
			return $"[+:{string.Join(",", DeclAdd)} | -:{string.Join(",", DeclDeny)}]";
		}
	}
}
