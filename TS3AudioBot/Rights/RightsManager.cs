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
	using CommandSystem;
	using Helper;
	using Nett;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using TS3Client;

	/// <summary>Permission system of the bot.</summary>
	public class RightsManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private const int RuleLevelSize = 2;

		public CommandManager CommandManager { get; set; }

		private bool needsRecalculation;
		private readonly Cache<string, ExecuteContext> cachedRights;
		private readonly RightsManagerData rightsManagerData;
		private RightsRule rootRule;
		private RightsRule[] rules;
		private readonly HashSet<string> registeredRights;

		// Required Matcher Data:
		// This variables save whether the current rights setup has at least one rule that
		// need a certain additional information.
		// This will save us from making unnecessary query calls.
		// TODO:
		private bool needsAvailableGroups = true;
		private bool needsAvailableChanGroups = true;

		public RightsManager(RightsManagerData rmd)
		{
			Util.Init(out cachedRights);
			Util.Init(out registeredRights);
			rightsManagerData = rmd;
		}

		public void Initialize()
		{
			RegisterRights(CommandManager.AllRights);
			RegisterRights(MainCommands.RightHighVolume, MainCommands.RightDeleteAllPlaylists);
			if (!ReadFile())
				Log.Error("Could not read Permission file.");
		}

		public void RegisterRights(params string[] rights) => RegisterRights((IEnumerable<string>)rights);
		public void RegisterRights(IEnumerable<string> rights)
		{
			// TODO validate right names
			registeredRights.UnionWith(rights);
			needsRecalculation = true;
		}

		public void UnregisterRights(params string[] rights) => UnregisterRights((IEnumerable<string>)rights);
		public void UnregisterRights(IEnumerable<string> rights)
		{
			// TODO validate right names
			// optionally expand
			registeredRights.ExceptWith(rights);
			needsRecalculation = true;
		}

		// TODO: b_client_permissionoverview_view
		public bool HasAllRights(CallerInfo caller, InvokerData invoker, TeamspeakControl ts, params string[] requestedRights)
		{
			var ctx = GetRightsContext(caller, invoker, ts);
			var normalizedRequest = ExpandRights(requestedRights);
			return ctx.DeclAdd.IsSupersetOf(normalizedRequest);
		}

		public string[] GetRightsSubset(CallerInfo caller, InvokerData invoker, TeamspeakControl ts, params string[] requestedRights)
		{
			var ctx = GetRightsContext(caller, invoker, ts);
			var normalizedRequest = ExpandRights(requestedRights);
			return ctx.DeclAdd.Intersect(normalizedRequest).ToArray();
		}

		private ExecuteContext GetRightsContext(CallerInfo caller, InvokerData invoker, TeamspeakControl ts)
		{
			if (needsRecalculation)
			{
				cachedRights.Invalidate();
				needsRecalculation = false;
				ReadFile();
			}

			ExecuteContext execCtx;
			if (invoker != null)
			{
				if (cachedRights.TryGetValue(invoker.ClientUid, out execCtx))
				{
					// TODO check if all fields are same
					// if yes => returen
					// if no => delete from cache
					return execCtx;
				}

				execCtx = new ExecuteContext();

				// Get Required Matcher Data:
				// In this region we will iteratively go through different possibilities to obtain
				// as much data as we can about our invoker.
				// For this step we will prefer query calls which can give us more than one information
				// at once and lazily fall back to other calls as long as needed.

				ulong[] availableGroups = null;
				if (ts != null)
				{
					if (invoker.ClientId.HasValue &&
						(needsAvailableGroups || needsAvailableChanGroups))
					{
						var result = ts.GetClientInfoById(invoker.ClientId.Value);
						if (result.Ok)
						{
							availableGroups = result.Value.ServerGroups;
							execCtx.ChannelGroupId = result.Value.ChannelGroup;
						}
					}

					if (needsAvailableGroups && invoker.DatabaseId.HasValue && availableGroups == null)
					{
						var result = ts.GetClientServerGroups(invoker.DatabaseId.Value);
						if (result.Ok)
							availableGroups = result.Value;
					}
				}

				if (availableGroups != null)
					execCtx.AvailableGroups = availableGroups;
				execCtx.ClientUid = invoker.ClientUid;
				execCtx.Visibiliy = invoker.Visibiliy;
				execCtx.ApiToken = invoker.Token;
			}
			else
			{
				execCtx = new ExecuteContext();
			}
			execCtx.IsApi = caller.ApiCall;

			ProcessNode(rootRule, execCtx);

			if (execCtx.MatchingRules.Count == 0)
				return execCtx;

			foreach (var rule in execCtx.MatchingRules)
				execCtx.DeclAdd.UnionWith(rule.DeclAdd);

			if (invoker != null)
				cachedRights.Store(invoker.ClientUid, execCtx);

			return execCtx;
		}

		private static bool ProcessNode(RightsRule rule, ExecuteContext ctx)
		{
			// check if node matches
			if (!rule.HasMatcher()
				|| (ctx.Host != null && rule.MatchHost.Contains(ctx.Host))
				|| (ctx.ClientUid != null && rule.MatchClientUid.Contains(ctx.ClientUid))
				|| (ctx.AvailableGroups.Length > 0 && rule.MatchClientGroupId.Overlaps(ctx.AvailableGroups))
				|| (ctx.ChannelGroupId.HasValue && rule.MatchChannelGroupId.Contains(ctx.ChannelGroupId.Value))
				|| (ctx.ApiToken != null && rule.MatchToken.Contains(ctx.ApiToken))
				|| (ctx.IsApi == rule.MatchIsApi)
				|| (ctx.Visibiliy.HasValue && rule.MatchVisibility.Contains(ctx.Visibiliy.Value)))
			{
				bool hasMatchingChild = false;
				foreach (var child in rule.ChildrenRules)
					hasMatchingChild |= ProcessNode(child, ctx);

				if (!hasMatchingChild)
					ctx.MatchingRules.Add(rule);
				return true;
			}
			return false;
		}

		// Loading and Parsing

		public bool ReadFile()
		{
			try
			{
				if (!File.Exists(rightsManagerData.RightsFile))
				{
					Log.Info("No rights file found. Creating default.");
					using (var fs = File.OpenWrite(rightsManagerData.RightsFile))
					using (var data = Util.GetEmbeddedFile("TS3AudioBot.Rights.DefaultRights.toml"))
						data.CopyTo(fs);
				}

				var table = Toml.ReadFile(rightsManagerData.RightsFile);
				var ctx = new ParseContext();
				RecalculateRights(table, ctx);
				foreach (var err in ctx.Errors)
					Log.Error(err);
				foreach (var warn in ctx.Warnings)
					Log.Warn(warn);
				return ctx.Errors.Count == 0;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "The rights file could not be parsed");
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
					strb.Append(string.Join("\n", rules.Select(x => x.ToString())));
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
			rules = Array.Empty<RightsRule>();

			rootRule = new RightsRule();
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

			if (!NormalizeRule(parseCtx))
				return;

			FlattenGroups(parseCtx);

			FlattenRules(rootRule);

			rules = parseCtx.Rules;
		}

		private HashSet<string> ExpandRights(IEnumerable<string> rights)
		{
			var rightsExpanded = new HashSet<string>();
			foreach (var right in rights)
			{
				int index = right.IndexOf('*');
				if (index < 0)
				{
					// Rule does not contain any wildcards
					rightsExpanded.Add(right);
				}
				else if (index != 0 && right[index - 1] != '.')
				{
					// Do not permit misused wildcards
					throw new ArgumentException($"The right \"{right}\" has a misused wildcard.");
				}
				else if (index == 0)
				{
					// We are done here when including every possible right
					rightsExpanded.UnionWith(registeredRights);
					break;
				}
				else
				{
					// Add all rights which expand from that wildcard
					string subMatch = right.Substring(0, index - 1);
					rightsExpanded.UnionWith(registeredRights.Where(x => x.StartsWith(subMatch)));
				}
			}
			return rightsExpanded;
		}

		/// <summary>
		/// Removes rights which are in the Add and Deny category.
		/// Expands wildcard declarations to all explicit declarations.
		/// </summary>
		/// <param name="ctx">The parsing context for the current file processing.</param>
		private bool NormalizeRule(ParseContext ctx)
		{
			bool hasErrors = false;

			foreach (var rule in ctx.Rules)
			{
				var denyNormalized = ExpandRights(rule.DeclDeny);
				rule.DeclDeny = denyNormalized.ToArray();
				var addNormalized = ExpandRights(rule.DeclAdd);
				addNormalized.ExceptWith(rule.DeclDeny);
				rule.DeclAdd = addNormalized.ToArray();

				var undeclared = rule.DeclAdd.Except(registeredRights)
					.Concat(rule.DeclDeny.Except(registeredRights));
				foreach (var right in undeclared)
				{
					ctx.Errors.Add($"Right \"{right}\" is not registered.");
					hasErrors = true;
				}
			}

			return !hasErrors;
		}

		/// <summary>
		/// Checks that each group name can be uniquely identified when resolving.
		/// </summary>
		/// <param name="ctx">The parsing context for the current file processing.</param>
		private static bool ValidateUniqueGroupNames(ParseContext ctx)
		{
			bool hasErrors = false;

			foreach (var checkGroup in ctx.Groups)
			{
				// check that the name is unique
				var parent = checkGroup.Parent;
				while (parent != null)
				{
					foreach (var cmpGroup in parent.ChildrenGroups)
					{
						if (cmpGroup != checkGroup
							&& cmpGroup.Name == checkGroup.Name)
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

		/// <summary>
		/// Resolves all include strings to their representative object each.
		/// </summary>
		/// <param name="ctx">The parsing context for the current file processing.</param>
		private static bool ResolveIncludes(ParseContext ctx)
		{
			bool hasErrors = false;

			foreach (var decl in ctx.Declarations)
				hasErrors |= !decl.ResolveIncludes(ctx);

			return !hasErrors;
		}

		/// <summary>
		/// Checks if group includes form a cyclic dependency.
		/// </summary>
		/// <param name="ctx">The parsing context for the current file processing.</param>
		private static bool CheckCyclicGroupDependencies(ParseContext ctx)
		{
			bool hasErrors = false;

			foreach (var checkGroup in ctx.Groups)
			{
				var included = new HashSet<RightsGroup>();
				var remainingIncludes = new Queue<RightsGroup>();
				remainingIncludes.Enqueue(checkGroup);

				while (remainingIncludes.Count > 0)
				{
					var include = remainingIncludes.Dequeue();
					included.Add(include);
					foreach (var newInclude in include.Includes)
					{
						if (newInclude == checkGroup)
						{
							hasErrors = true;
							ctx.Errors.Add($"Group \"{checkGroup.Name}\" has a cyclic include hierarchy.");
							break;
						}
						if (!included.Contains(newInclude))
							remainingIncludes.Enqueue(newInclude);
					}
				}
			}

			return !hasErrors;
		}

		/// <summary>
		/// Generates hierarchical values for the <see cref="RightsDecl.Level"/> field
		/// for all rules. This value represents which rule is more specified when
		/// merging two rule in order to prioritize rights.
		/// </summary>
		/// <param name="root">The root element of the hierarchy tree.</param>
		/// <param name="level">The base level for the root element.</param>
		private static void BuildLevel(RightsDecl root, int level = 0)
		{
			root.Level = level;
			if (root is RightsRule rootRule)
				foreach (var child in rootRule.Children)
					BuildLevel(child, level + RuleLevelSize);
		}

		/// <summary>
		/// Checks groups and rules for common mistakes and unusual declarations.
		/// Found stuff will be added as warnings.
		/// </summary>
		/// <param name="ctx">The parsing context for the current file processing.</param>
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
			foreach (var decl in ctx.Groups)
			{
				if (decl.Includes.Length == 0 && decl.DeclDeny.Length > 0)
					ctx.Warnings.Add("Rule with \"-\" declaration but no include to override");
			}
			var root = ctx.Rules.First(x => x.Parent == null);
			if (root.Includes.Length == 0 && root.DeclDeny.Length > 0)
				ctx.Warnings.Add("Root rule \"-\" declaration has no effect");

			// check if rule has no matcher
			foreach (var rule in ctx.Rules)
			{
				if (!rule.HasMatcher() && rule.Parent != null)
					ctx.Warnings.Add("Rule has no matcher");
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
				ctx.Warnings.Add($"Group \"{uGroup.Name}\" is never included in a rule");
		}

		/// <summary>
		/// Summs up all includes for each group and includes them directly into the
		/// <see cref="RightsDecl.DeclAdd"/> and <see cref="RightsDecl.DeclDeny"/>.
		/// </summary>
		/// <param name="ctx">The parsing context for the current file processing.</param>
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

					item.MergeGroups(item.Includes);
					item.Includes = null;
				}
				else
				{
					notReachable.Enqueue(item);
				}
			}
		}

		/// <summary>
		/// Summs up all includes and parent rule declarations for each rule and includes them
		/// directly into the <see cref="RightsDecl.DeclAdd"/> and <see cref="RightsDecl.DeclDeny"/>.
		/// </summary>
		/// <param name="root">The root element of the hierarchy tree.</param>
		private static void FlattenRules(RightsRule root)
		{
			if (root.Parent != null)
				root.MergeGroups(root.Parent);
			root.MergeGroups(root.Includes);
			root.Includes = null;

			foreach (var child in root.ChildrenRules)
				FlattenRules(child);
		}
	}

	internal class ExecuteContext
	{
		public string Host { get; set; }
		public ulong[] AvailableGroups { get; set; } = Array.Empty<ulong>();
		public ulong? ChannelGroupId { get; set; }
		public string ClientUid { get; set; }
		public bool IsApi { get; set; }
		public string ApiToken { get; set; }
		public TextMessageTargetMode? Visibiliy { get; set; }

		public List<RightsRule> MatchingRules { get; } = new List<RightsRule>();

		public HashSet<string> DeclAdd { get; } = new HashSet<string>();
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

	public class RightsManagerData : ConfigData
	{
		[Info("Path to the config file", "rights.toml")]
		public string RightsFile { get; set; }
	}
}
