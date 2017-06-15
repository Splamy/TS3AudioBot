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

		public override bool ParseKey(string key, TomlObject tomlObj, List<RightsDecl> rules)
		{
			if (base.ParseKey(key, tomlObj, rules))
				return true;

			switch (key)
			{
			case "host":
				MatchHost = TomlTools.GetValues<string>(tomlObj);
				if (MatchHost == null)
					Log.Write(Log.Level.Error, "<host> Field has invalid data.");
				break;
			case "groupid":
				MatchClientGroupId = TomlTools.GetValues<ulong>(tomlObj);
				if (MatchClientGroupId == null)
					Log.Write(Log.Level.Error, "<groupid> Field has invalid data.");
				break;
			case "useruid":
				MatchClientUid = TomlTools.GetValues<string>(tomlObj);
				if (MatchClientUid == null)
					Log.Write(Log.Level.Error, "<useruid> Field has invalid data.");
				break;
			case "perm":
				MatchPermission = TomlTools.GetValues<string>(tomlObj);
				if (MatchPermission == null)
					Log.Write(Log.Level.Error, "<perm> Field has invalid data.");
				break;
			case "rule":
				if (tomlObj.TomlType == TomlObjectType.ArrayOfTables)
				{
					var childTables = (TomlTableArray)tomlObj;
					foreach (var childTable in childTables.Items)
					{
						var rule = new RightsRule();
						Children.Add(rule);
						rule.Parent = this;
						rule.ParseChilden(childTable, rules);
					}
				}
				else
				{
					Log.Write(Log.Level.Error, "Misused key with reserved name \"rule\".");
				}
				break;
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
						group.ParseChilden(childTable, rules);
						return true;
					}
					else
					{
						Log.Write(Log.Level.Error, "Misused key for group declaration: {0}.", key);
					}
				}
				return false;
			}

			return true;
		}

		public override RightsGroup ResolveGroup(string groupName)
		{
			foreach (var child in Children)
			{
				if (child is RightsGroup && ((RightsGroup)child).Name == groupName)
					return (RightsGroup)child;
			}
			if (Parent == null)
				return null;
			return Parent.ResolveGroup(groupName);
		}
	}
}
