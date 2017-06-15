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

		public virtual bool ParseKey(string key, TomlObject tomlObj, ParseContext ctx)
		{
			switch (key)
			{
			case "+":
				DeclAdd = TomlTools.GetValues<string>(tomlObj);
				if (DeclAdd == null) ctx.Errors.Add("<+> Field has invalid data.");
				return true;
			case "-":
				DeclDeny = TomlTools.GetValues<string>(tomlObj);
				if (DeclDeny == null) ctx.Errors.Add("<-> Field has invalid data.");
				return true;
			case "include":
				includeNames = TomlTools.GetValues<string>(tomlObj);
				if (includeNames == null) ctx.Errors.Add("<include> Field has invalid data.");
				return true;
			default:
				return false;
			}
		}

		public bool ParseChilden(TomlTable tomlObj, ParseContext ctx)
		{
			Id = ctx.Declarations.Count;
			ctx.Declarations.Add(this);
			bool hasErrors = false;

			foreach (var item in tomlObj)
			{
				if (!ParseKey(item.Key, item.Value, ctx))
				{
					ctx.Errors.Add($"Unrecognized key <{item.Key}>.");
					hasErrors = true;
				}
			}
			FillNull();
			return !hasErrors;
		}

		public abstract RightsGroup ResolveGroup(string groupName, ParseContext ctx);

		public bool ResolveIncludes(ParseContext ctx)
		{
			bool hasErrors = false;
			if (includeNames != null)
			{
				Includes = includeNames.Select(x => ResolveGroup(x, ctx)).ToArray();
				for (int i = 0; i < includeNames.Length; i++)
					if (Includes[i] == null)
					{
						ctx.Errors.Add($"Could not find group \"{includeNames[i]}\" to include.");
						hasErrors = true;
					}
				includeNames = null;
			}
			return !hasErrors;
		}
	}
}
