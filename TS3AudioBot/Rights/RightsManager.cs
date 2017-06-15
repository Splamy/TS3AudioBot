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
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3AudioBot.Helper;

	public class RightsManager
	{
		private const int RuleLevelSize = 2;

		private RightsManagerData rightsManagerData;
		private RightsRule[] Rules;

		public RightsManager(RightsManagerData rmd)
		{
			Log.RegisterLogger("[%T]%L: %M", "", Console.WriteLine);
			rightsManagerData = rmd;
			RecalculateRights();
		}

		public bool HasRight(InvokerData inv)
		{
			return true;
		}

		// Loading and Parsing

		private TomlTable ReadFile()
		{
			try
			{
				return Toml.ReadFile(rightsManagerData.RightsFile);
			}
			catch (Exception ex)
			{
				Log.Write(Log.Level.Error, "The rights file could not be parsed: {0}", ex);
				return null;
			}
		}

		private bool RecalculateRights()
		{
			Rules = new RightsRule[0];

			var table = ReadFile();
			if (table == null)
				return false;

			var declarations = new List<RightsDecl>();
			var rootRule = new RightsRule();
			rootRule.ParseChilden(table, declarations);

			var rightGroups = declarations.OfType<RightsGroup>().ToArray();
			var rightRules = declarations.OfType<RightsRule>().ToArray();

			if (!ValidateUniqueGroupNames(rightGroups))
				return false;

			if (!ResolveIncludes(declarations))
				return false;

			if (!CheckCyclicGroupDependencies(rightGroups))
				return false;

			BuildLevel(rootRule);

			LintDeclarations(declarations);

			FlattenGroups(rightGroups);

			FlattenRules(rootRule);

			Rules = rightRules;
			return true;
		}

		private static bool ValidateUniqueGroupNames(RightsGroup[] groups)
		{
			bool hasErrors = false;

			foreach (var checkGroup in groups)
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
							Log.Write(Log.Level.Error, "Ambiguous group name: {0}", checkGroup.Name);
							hasErrors = true;
						}
					}
					parent = parent.Parent;
				}
			}

			return !hasErrors;
		}

		private static bool ResolveIncludes(List<RightsDecl> declarations)
		{
			bool hasErrors = false;

			foreach (var decl in declarations)
				hasErrors |= !decl.ResolveIncludes();

			return !hasErrors;
		}

		private static bool CheckCyclicGroupDependencies(RightsGroup[] groups)
		{
			bool hasErrors = false;

			foreach (var checkGroup in groups)
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
							Log.Write(Log.Level.Error, "Group \"{0}\" has a cyclic include hierachy.", checkGroup.Name);
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

		private static void LintDeclarations(List<RightsDecl> declarations)
		{
			// TODO

			// check if <+> contains <-> decl

			// top level <-> declaration is useless

			// check if rule has no matcher

			// check for impossible combinations uid + uid, server + server, perm + perm ?

			// check for unused group
		}

		private static void MergeGroups(RightsDecl main, params RightsDecl[] merge)
		{
			// main.+ = (include+ - main-) + main+
			// main.- = main-
			foreach (var include in merge)
				main.DeclAdd = include.DeclAdd.Except(main.DeclDeny).Concat(main.DeclAdd).Distinct().ToArray();
		}

		private static void FlattenGroups(RightsGroup[] groups)
		{
			var notReachable = new Queue<RightsGroup>(groups);
			var currentlyReached = new HashSet<RightsGroup>(groups.Where(x => x.Includes.Length == 0));

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
