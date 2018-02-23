// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Rights
{
	using Nett;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3Client;

	// Adding a new Matcher:
	// 1) Add public MatchHashSet
	// 2) Add To Has Matches condition when empty
	// 3) Add To FillNull when not declared
	// 4) Add new case to ParseKey switch
	// 5) Add match condition to RightManager.ProcessNode
	// 6) Add Property in the ExecuteContext class
	// 7) Set value in RightManager.GetRightsContext

	internal class RightsRule : RightsDecl
	{
		public List<RightsDecl> Children { get; set; }
		public IEnumerable<RightsRule> ChildrenRules => Children.OfType<RightsRule>();
		public IEnumerable<RightsGroup> ChildrenGroups => Children.OfType<RightsGroup>();

		public HashSet<string> MatchHost { get; set; }
		public HashSet<string> MatchClientUid { get; set; }
		public HashSet<ulong> MatchClientGroupId { get; set; }
		public HashSet<ulong> MatchChannelGroupId { get; set; }
		public HashSet<string> MatchPermission { get; set; }
		public HashSet<string> MatchToken { get; set; }
		public bool? MatchIsApi { get; set; }
		public TextMessageTargetMode[] MatchVisibility { get; set; }

		public RightsRule()
		{
			Children = new List<RightsDecl>();
		}

		public bool HasMatcher()
		{
			return MatchClientGroupId.Count > 0
				|| MatchClientUid.Count > 0
				|| MatchHost.Count > 0
				|| MatchPermission.Count > 0
				|| MatchChannelGroupId.Count > 0
				|| MatchToken.Count > 0
				|| MatchIsApi.HasValue
				|| MatchVisibility.Length > 0;
		}

		public override void FillNull()
		{
			base.FillNull();
			if (MatchHost == null) MatchHost = new HashSet<string>();
			if (MatchClientUid == null) MatchClientUid = new HashSet<string>();
			if (MatchClientGroupId == null) MatchClientGroupId = new HashSet<ulong>();
			if (MatchChannelGroupId == null) MatchChannelGroupId = new HashSet<ulong>();
			if (MatchPermission == null) MatchPermission = new HashSet<string>();
			if (MatchToken == null) MatchToken = new HashSet<string>();
			if (MatchVisibility == null) MatchVisibility = Array.Empty<TextMessageTargetMode>();
		}

		public override bool ParseKey(string key, TomlObject tomlObj, ParseContext ctx)
		{
			if (base.ParseKey(key, tomlObj, ctx))
				return true;

			switch (key)
			{
			case "host":
				var host = TomlTools.GetValues<string>(tomlObj);
				if (host == null) ctx.Errors.Add("<host> Field has invalid data.");
				else MatchHost = new HashSet<string>(host);
				return true;
			case "groupid":
				var groupid = TomlTools.GetValues<ulong>(tomlObj);
				if (groupid == null) ctx.Errors.Add("<groupid> Field has invalid data.");
				else MatchClientGroupId = new HashSet<ulong>(groupid);
				return true;
			case "channelgroupid":
				var cgroupid = TomlTools.GetValues<ulong>(tomlObj);
				if (cgroupid == null) ctx.Errors.Add("<channelgroupid> Field has invalid data.");
				else MatchChannelGroupId = new HashSet<ulong>(cgroupid);
				return true;
			case "useruid":
				var useruid = TomlTools.GetValues<string>(tomlObj);
				if (useruid == null) ctx.Errors.Add("<useruid> Field has invalid data.");
				else MatchClientUid = new HashSet<string>(useruid);
				return true;
			case "perm":
				var perm = TomlTools.GetValues<string>(tomlObj);
				if (perm == null) ctx.Errors.Add("<perm> Field has invalid data.");
				else MatchPermission = new HashSet<string>(perm);
				return true;
			case "apitoken":
				var apitoken = TomlTools.GetValues<string>(tomlObj);
				if (apitoken == null) ctx.Errors.Add("<apitoken> Field has invalid data.");
				else MatchToken = new HashSet<string>(apitoken);
				return true;
			case "isapi":
				if (!TomlTools.TryGetValue<bool>(tomlObj, out var isapi)) ctx.Errors.Add("<isapi> Field has invalid data.");
				else MatchIsApi = isapi;
				return true;
			case "visibility":
				var visibility = TomlTools.GetValues<TextMessageTargetMode>(tomlObj);
				if (visibility == null) ctx.Errors.Add("<visibility> Field has invalid data.");
				else MatchVisibility = visibility;
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
