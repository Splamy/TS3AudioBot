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
	using Helper;
	using Nett;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	public class RightsManager
	{
		private const int RuleLevelSize = 2;

		private RightsManagerData rightsManagerData;
		private RightsRule[] Rules;

		public RightsManager(RightsManagerData rmd)
		{
			rightsManagerData = rmd;
		}

		public bool HasRight(InvokerData inv)
		{
			return true;
		}

		// Loading and Parsing

		public bool ReadFile()
		{
			try
			{
				var table = Toml.ReadFile(rightsManagerData.RightsFile);
				var ctx = new ParseContext();
				RecalculateRights(table, ctx);
				foreach (var err in ctx.Errors)
					Log.Write(Log.Level.Error, err);
				foreach (var warn in ctx.Warnings)
					Log.Write(Log.Level.Warning, warn);
				return ctx.Errors.Count == 0;
			}
			catch (Exception ex)
			{
				Log.Write(Log.Level.Error, "The rights file could not be parsed: {0}", ex);
				return false;
			}
		}

		public R<string> ReadText(string text)
		{
			try
			{
				var table = Toml.ReadString(text);
				var ctx = new ParseContext();
				RecalculateRights(table, ctx);
				var strb = new StringBuilder();
				foreach (var warn in ctx.Warnings)
					strb.Append("WRN: ").AppendLine(warn);
				if (ctx.Errors.Count == 0)
				{
					strb.Append(string.Join("\n", Rules.Select(x => x.ToString())));
					if (strb.Length > 900)
						strb.Length = 900;
					return R<string>.OkR(strb.ToString());
				}
				else
				{
					foreach (var err in ctx.Errors)
						strb.Append("ERR: ").AppendLine(err);
					if (strb.Length > 900)
						strb.Length = 900;
					return R<string>.Err(strb.ToString());
				}
			}
			catch (Exception ex)
			{
				return R<string>.Err("The rights file could not be parsed: " + ex.Message);
			}
		}

		private void RecalculateRights(TomlTable table, ParseContext parseCtx)
		{
			Rules = new RightsRule[0];

			var rootRule = new RightsRule();
			if (!rootRule.ParseChilden(table, parseCtx))
				return;

			parseCtx.SplitDeclarations();

			if (!ValidateUniqueGroupNames(parseCtx))
				return;

			if (!ResolveIncludes(parseCtx))
				return;

			if (!CheckCyclicGroupDependencies(parseCtx))
				return;

			BuildLevel(rootRule);

			LintDeclarations(parseCtx);

			FlattenGroups(parseCtx);

			FlattenRules(rootRule);

			Rules = parseCtx.Rules;
		}

		private static bool ValidateUniqueGroupNames(ParseContext ctx)
		{
			bool hasErrors = false;

			foreach (var checkGroup in ctx.Groups)
			{
				// check that the name is unique
				var parent = checkGroup.Parent;
				while (parent != null)
				{
					foreach (var cmpGroup in parent.Children)
					{
						if (cmpGroup != checkGroup
							&& cmpGroup is RightsGroup
							&& ((RightsGroup)cmpGroup).Name == checkGroup.Name)
						{
							ctx.Errors.Add($"Ambiguous group name: {checkGroup.Name}");
							hasErrors = true;
						}
					}
					parent = parent.Parent;
				}
			}

			return !hasErrors;
		}

		private static bool ResolveIncludes(ParseContext ctx)
		{
			bool hasErrors = false;

			foreach (var decl in ctx.Declarations)
				hasErrors |= !decl.ResolveIncludes(ctx);

			return !hasErrors;
		}

		private static bool CheckCyclicGroupDependencies(ParseContext ctx)
		{
			bool hasErrors = false;

			foreach (var checkGroup in ctx.Groups)
			{
				var included = new HashSet<RightsGroup>();
				var remainingIncludes = new Queue<RightsGroup>();
				remainingIncludes.Enqueue(checkGroup);

				while (remainingIncludes.Any())
				{
					var include = remainingIncludes.Dequeue();
					included.Add(include);
					foreach (var newInclude in include.Includes)
					{
						if (newInclude == checkGroup)
						{
							hasErrors = true;
							ctx.Errors.Add($"Group \"{checkGroup.Name}\" has a cyclic include hierachy.");
							break;
						}
						if (!included.Contains(newInclude))
							remainingIncludes.Enqueue(newInclude);
					}
				}
			}

			return !hasErrors;
		}

		private static void BuildLevel(RightsDecl decl, int level = 0)
		{
			decl.Level = level;
			if (decl is RightsRule)
				foreach (var child in ((RightsRule)decl).Children)
					BuildLevel(child, level + RuleLevelSize);
		}

		private static void LintDeclarations(ParseContext ctx)
		{
			// check if <+> contains <-> decl
			foreach (var decl in ctx.Declarations)
			{
				var uselessAdd = decl.DeclAdd.Intersect(decl.DeclDeny).ToArray();
				foreach (var uAdd in uselessAdd)
					ctx.Warnings.Add($"Rule has declaration \"{uAdd}\" in \"+\" and \"-\"");
			}

			// top level <-> declaration is useless
			foreach (var decl in ctx.Declarations)
			{
				if (decl.Includes.Length == 0 && decl.DeclDeny.Length > 0)
					ctx.Warnings.Add($"Rule with \"-\" declaration but no include to override");
			}

			// check if rule has no matcher
			foreach (var rule in ctx.Rules)
			{
				if (rule.MatchClientGroupId.Length == 0
					&& rule.MatchClientUid.Length == 0
					&& rule.MatchHost.Length == 0
					&& rule.MatchPermission.Length == 0
					&& rule.Parent != null)
					ctx.Warnings.Add($"Rule has no matcher");
			}

			// check for impossible combinations uid + uid, server + server, perm + perm ?
			// TODO

			// check for unused group
			var unusedGroups = new HashSet<RightsGroup>(ctx.Groups);
			foreach (var decl in ctx.Declarations)
			{
				foreach (var include in decl.Includes)
				{
					if (unusedGroups.Contains(include))
						unusedGroups.Remove(include);
				}
			}
			foreach (var uGroup in unusedGroups)
				ctx.Warnings.Add($"Group \"{uGroup.Name}\" is nerver included in a rule");
		}

		private static void MergeGroups(RightsDecl main, params RightsDecl[] merge)
		{
			// main.+ = (include+ - main-) + main+
			// main.- = main-
			foreach (var include in merge)
				main.DeclAdd = include.DeclAdd.Except(main.DeclDeny).Concat(main.DeclAdd).Distinct().ToArray();
		}

		private static void FlattenGroups(ParseContext ctx)
		{
			var notReachable = new Queue<RightsGroup>(ctx.Groups);
			var currentlyReached = new HashSet<RightsGroup>(ctx.Groups.Where(x => x.Includes.Length == 0));

			while (notReachable.Count > 0)
			{
				var item = notReachable.Dequeue();
				if (currentlyReached.IsSupersetOf(item.Includes))
				{
					currentlyReached.Add(item);

					MergeGroups(item, item.Includes);
					item.Includes = null;
				}
				else
				{
					notReachable.Enqueue(item);
				}
			}
		}

		private static void FlattenRules(RightsRule root)
		{
			if (root.Parent != null)
				MergeGroups(root, root.Parent);
			MergeGroups(root, root.Includes);
			root.Includes = null;

			foreach (var child in root.Children)
				if (child is RightsRule)
					FlattenRules((RightsRule)child);
		}
	}

	internal class ParseContext
	{
		public List<RightsDecl> Declarations { get; }
		public RightsGroup[] Groups { get; private set; }
		public RightsRule[] Rules { get; private set; }
		public List<string> Errors { get; }
		public List<string> Warnings { get; }

		public ParseContext()
		{
			Declarations = new List<RightsDecl>();
			Errors = new List<string>();
			Warnings = new List<string>();
		}

		public void SplitDeclarations()
		{
			Groups = Declarations.OfType<RightsGroup>().ToArray();
			Rules = Declarations.OfType<RightsRule>().ToArray();
		}
	}

	struct DeclLevel
	{
		public int Level;
		public bool Add;
	}

	public class RightsManagerData : ConfigData
	{
		[Info("Path to the config file", "rights.toml")]
		public string RightsFile { get; set; }
	}
}
