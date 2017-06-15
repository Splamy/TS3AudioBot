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

namespace TS3AudioBot.Rights
{
	using Nett;
	using System.Collections.Generic;

	internal class RightsRule : RightsDecl
	{
		public List<RightsDecl> Children { get; set; }
		public Dictionary<string, DeclLevel> DeclMap { get; }

		public string[] MatchHost { get; set; }
		public string[] MatchClientUid { get; set; }
		public ulong[] MatchClientGroupId { get; set; }
		public string[] MatchPermission { get; set; }

		public RightsRule()
		{
			Children = new List<RightsDecl>();
			DeclMap = new Dictionary<string, DeclLevel>();
		}

		public override void FillNull()
		{
			base.FillNull();
			if (MatchHost == null) MatchHost = new string[0];
			if (MatchClientUid == null) MatchClientUid = new string[0];
			if (MatchClientGroupId == null) MatchClientGroupId = new ulong[0];
			if (MatchPermission == null) MatchPermission = new string[0];
		}

		public override bool ParseKey(string key, TomlObject tomlObj, ParseContext ctx)
		{
			if (base.ParseKey(key, tomlObj, ctx))
				return true;

			switch (key)
			{
			case "host":
				MatchHost = TomlTools.GetValues<string>(tomlObj);
				if (MatchHost == null) ctx.Errors.Add("<host> Field has invalid data.");
				return true;
			case "groupid":
				MatchClientGroupId = TomlTools.GetValues<ulong>(tomlObj);
				if (MatchClientGroupId == null) ctx.Errors.Add("<groupid> Field has invalid data.");
				return true;
			case "useruid":
				MatchClientUid = TomlTools.GetValues<string>(tomlObj);
				if (MatchClientUid == null) ctx.Errors.Add("<useruid> Field has invalid data.");
				return true;
			case "perm":
				MatchPermission = TomlTools.GetValues<string>(tomlObj);
				if (MatchPermission == null) ctx.Errors.Add("<perm> Field has invalid data.");
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
			foreach (var child in Children)
			{
				if (child is RightsGroup && ((RightsGroup)child).Name == groupName)
					return (RightsGroup)child;
			}
			if (Parent == null)
				return null;
			return Parent.ResolveGroup(groupName, ctx);
		}

		public override string ToString()
		{
			return $"[+:{string.Join(",", DeclAdd)} | -:{string.Join(",", DeclDeny)}]";
		}
	}
}
