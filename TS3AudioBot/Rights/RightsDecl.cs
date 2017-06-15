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
	using System.Linq;

	internal abstract class RightsDecl
	{
		public int Id { get; private set; }
		public int Level { get; set; }

		public RightsRule Parent { get; set; }
		private string[] includeNames;
		public RightsGroup[] Includes { get; set; }

		public string[] DeclAdd { get; set; }
		public string[] DeclDeny { get; set; }

		public RightsDecl() { }

		public virtual void FillNull()
		{
			if (includeNames == null) includeNames = new string[0];
			if (DeclAdd == null) DeclAdd = new string[0];
			if (DeclDeny == null) DeclDeny = new string[0];
		}

		public virtual bool ParseKey(string key, TomlObject tomlObj, List<RightsDecl> rules)
		{
			switch (key)
			{
			case "+":
				DeclAdd = TomlTools.GetValues<string>(tomlObj);
				if (DeclAdd == null)
					Log.Write(Log.Level.Error, "<+> Field has invalid data.");
				break;
			case "-":
				DeclDeny = TomlTools.GetValues<string>(tomlObj);
				if (DeclDeny == null)
					Log.Write(Log.Level.Error, "<-> Field has invalid data.");
				break;
			case "include":
				includeNames = TomlTools.GetValues<string>(tomlObj);
				if (includeNames == null)
					Log.Write(Log.Level.Error, "<include> Field has invalid data.");
				break;
			default: return false;
			}
			return true;
		}

		public void ParseChilden(TomlTable tomlObj, List<RightsDecl> rules)
		{
			Id = rules.Count;
			rules.Add(this);

			foreach (var item in tomlObj)
			{
				if (!ParseKey(item.Key, item.Value, rules))
				{
					Log.Write(Log.Level.Error, "Unrecognized key <{0}>.", item.Key);
				}
			}
			FillNull();
		}

		public abstract RightsGroup ResolveGroup(string groupName);

		public bool ResolveIncludes()
		{
			bool hasErrors = false;
			if (includeNames != null)
			{
				Includes = includeNames.Select(ResolveGroup).ToArray();
				for (int i = 0; i < includeNames.Length; i++)
					if (Includes[i] == null)
					{
						Log.Write(Log.Level.Error, "Could not find group \"{0}\" to include.", includeNames[i]);
						hasErrors = true;
					}
				includeNames = null;
			}
			return !hasErrors;
		}
	}
}
