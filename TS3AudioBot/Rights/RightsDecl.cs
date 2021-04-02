// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Nett;
using System;
using System.Collections.Generic;
using System.Linq;
using TS3AudioBot.Helper;

namespace TS3AudioBot.Rights
{
	internal abstract class RightsDecl
	{
		public int Id { get; private set; }
		public int Level { get; set; }

		public RightsRule? Parent { get; set; }
		private string[]? includeNames = null;
		public RightsGroup[] Includes { get; set; } = Array.Empty<RightsGroup>();

		public string[] DeclAdd { get; set; } = Array.Empty<string>();
		public string[] DeclDeny { get; set; } = Array.Empty<string>();

		public virtual bool ParseKey(string key, TomlObject tomlObj, ParseContext ctx)
		{
			switch (key)
			{
			case "+":
				if (!tomlObj.TryGetValueArray<string>(out var declAdd))
				{
					ctx.Errors.Add("<+> Field has invalid data.");
					declAdd = Array.Empty<string>();
				}
				DeclAdd = declAdd;
				return true;
			case "-":
				if (!tomlObj.TryGetValueArray<string>(out var declDeny))
				{
					ctx.Errors.Add("<-> Field has invalid data.");
					declDeny = Array.Empty<string>();
				}
				DeclDeny = declDeny;
				return true;
			case "include":
				if (!tomlObj.TryGetValueArray<string>(out var includeNames))
				{
					ctx.Errors.Add("<include> Field has invalid data.");
					includeNames = null;
				}
				this.includeNames = includeNames;
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
			return !hasErrors;
		}

		public abstract RightsGroup? ResolveGroup(string groupName, ParseContext ctx);

		/// <summary>
		/// Resolves all include strings to their representative object each.
		/// </summary>
		/// <param name="ctx">The parsing context for the current file processing.</param>
		public bool ResolveIncludes(ParseContext ctx)
		{
			bool hasErrors = false;
			if (includeNames != null)
			{
				Includes = includeNames.Select(x => ResolveGroup(x, ctx)).ToArray()!;
				for (int i = 0; i < includeNames.Length; i++)
				{
					if (Includes[i] is null)
					{
						ctx.Errors.Add($"Could not find group \"{includeNames[i]}\" to include.");
						hasErrors = true;
					}
				}
				includeNames = null;
			}
			return !hasErrors;
		}

		public void MergeGroups(IEnumerable<RightsDecl> merge)
		{
			// this.+ = (include+ - this-) + this+
			// this.- = this-
			foreach (var include in merge)
				MergeGroups(include);
		}

		public void MergeGroups(RightsDecl include)
		{
			DeclAdd = include.DeclAdd.Except(DeclDeny).Concat(DeclAdd).Distinct().ToArray();
		}
	}
}
