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
				var host = tomlObj.TryGetValueArray<string>();
				if (host is null) ctx.Errors.Add("<host> Field has invalid data.");
				else Matcher.Add(new MatchHost(host));
				return true;
			case "groupid":
				var servergroupid = tomlObj.TryGetValueArray<ulong>();
				if (servergroupid is null) ctx.Errors.Add("<groupid> Field has invalid data.");
				else Matcher.Add(new MatchServerGroupId(servergroupid.Select(ServerGroupId.To)));
				return true;
			case "channelgroupid":
				var channelgroupid = tomlObj.TryGetValueArray<ulong>();
				if (channelgroupid is null) ctx.Errors.Add("<channelgroupid> Field has invalid data.");
				else Matcher.Add(new MatchChannelGroupId(channelgroupid.Select(ChannelGroupId.To)));
				return true;
			case "useruid":
				var useruid = tomlObj.TryGetValueArray<string>();
				if (useruid is null) ctx.Errors.Add("<useruid> Field has invalid data.");
				else Matcher.Add(new MatchClientUid(useruid.Select(Uid.To)));
				return true;
			case "perm":
				var perm = tomlObj.TryGetValueArray<string>();
				if (perm is null) ctx.Errors.Add("<perm> Field has invalid data.");
				else Matcher.Add(new MatchPermission(perm, ctx));
				return true;
			case "apitoken":
				var apitoken = tomlObj.TryGetValueArray<string>();
				if (apitoken is null) ctx.Errors.Add("<apitoken> Field has invalid data.");
				else Matcher.Add(new MatchToken(apitoken));
				return true;
			case "bot":
				var bot = tomlObj.TryGetValueArray<string>();
				if (bot is null) ctx.Errors.Add("<bot> Field has invalid data.");
				else Matcher.Add(new MatchBot(bot));
				return true;
			case "isapi":
				if (!tomlObj.TryGetValue<bool>(out var isapi)) ctx.Errors.Add("<isapi> Field has invalid data.");
				else Matcher.Add(new MatchIsApi(isapi));
				return true;
			case "ip":
				var ip = tomlObj.TryGetValueArray<string>();
				if (ip is null) ctx.Errors.Add("<ip> Field has invalid data.");
				else
				{
					Matcher.Add(new MatchApiCallerIp(ip.Select(x =>
					{
						if (IPAddress.TryParse(x, out var ipa))
							return ipa;
						ctx.Errors.Add($"<ip> Field value '{x}' could not be parsed.");
						return null;
					}).Where(x => x != null)));
				}
				return true;
			case "visibility":
				var visibility = tomlObj.TryGetValueArray<TextMessageTargetMode>();
				if (visibility is null) ctx.Errors.Add("<visibility> Field has invalid data.");
				else Matcher.Add(new MatchVisibility(visibility));
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

		public override RightsGroup ResolveGroup(string groupName, ParseContext ctx)
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
