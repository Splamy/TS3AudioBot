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
using System.IO;
using System.Linq;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;
using TS3AudioBot.Helper;
using TS3AudioBot.Rights.Matchers;
using TS3AudioBot.Web.Api;
using TSLib;
using TSLib.Helper;
using TSLib.Messages;

namespace TS3AudioBot.Rights
{
	/// <summary>Permission system of the bot.</summary>
	public class RightsManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private const int RuleLevelSize = 2;

		private bool needsRecalculation;
		private readonly ConfRights config;
		private RightsRule rootRule;
		private readonly HashSet<string> registeredRights = new HashSet<string>();
		private readonly object rootRuleLock = new object();

		// Required Matcher Data:
		// This variables save whether the current rights setup has at least one rule that
		// need a certain additional information.
		// This will save us from making unnecessary query calls.
		private bool needsAvailableGroups = true;
		private bool needsAvailableChanGroups = true;
		private TsPermission[] needsPermOverview = Array.Empty<TsPermission>();

		public RightsManager(ConfRights config)
		{
			this.config = config;
			needsRecalculation = true;
		}

		public void SetRightsList(IEnumerable<string> rights)
		{
			// TODO validate right names
			registeredRights.Clear();
			registeredRights.UnionWith(rights);
			needsRecalculation = true;
		}

		public bool HasAllRights(ExecutionInformation info, params string[] requestedRights)
		{
			var ctx = GetRightsContext(info);
			var normalizedRequest = ExpandRights(requestedRights, registeredRights);
			return ctx.DeclAdd.IsSupersetOf(normalizedRequest);
		}

		public string[] GetRightsSubset(ExecutionInformation info, params string[] requestedRights)
		{
			var ctx = GetRightsContext(info);
			var normalizedRequest = ExpandRights(requestedRights, registeredRights);
			return ctx.DeclAdd.Intersect(normalizedRequest).ToArray();
		}

		private ExecuteContext GetRightsContext(ExecutionInformation info)
		{
			var localRootRule = TryGetRootSafe();

			if (info.TryGet<ExecuteContext>(out var execCtx))
				return execCtx;

			execCtx = new ExecuteContext();

			if (info.TryGet<ClientCall>(out var clientCall))
			{
				execCtx.ServerGroups = clientCall.ServerGroups;
				execCtx.ClientUid = clientCall.ClientUid;
				execCtx.Visibiliy = clientCall.Visibiliy;
				execCtx.IsApi = false;

				// Get Required Matcher Data:
				// In this region we will iteratively go through different possibilities to obtain
				// as much data as we can about our invoker.
				// For this step we will prefer query calls which can give us more than one information
				// at once and lazily fall back to other calls as long as needed.

				if (info.TryGet<Ts3Client>(out var ts) && info.TryGet<TsBaseFunctions>(out var tsClient))
				{
					ServerGroupId[] serverGroups = clientCall.ServerGroups;
					ChannelId? channelId = clientCall.ChannelId;
					ClientDbId? databaseId = clientCall.DatabaseId;
					ChannelGroupId? channelGroup = clientCall.ChannelGroup;

					if (clientCall.ClientId != null
						&& ((needsAvailableGroups && serverGroups is null)
							|| (needsAvailableChanGroups && channelGroup is null)
							|| (needsPermOverview.Length > 0 && (databaseId == null || channelId == null))
						)
					)
					{
						var result = ts.GetClientInfoById(clientCall.ClientId.Value);
						if (result.Ok)
						{
							serverGroups = result.Value.ServerGroups;
							channelGroup = result.Value.ChannelGroup;
							databaseId = result.Value.DatabaseId;
							channelId = result.Value.ChannelId;
						}
					}

					if (needsAvailableGroups && serverGroups is null)
					{
						if (databaseId == null)
						{
							var resultDbId = ts.GetClientDbIdByUid(clientCall.ClientUid);
							if (resultDbId.Ok)
							{
								databaseId = resultDbId.Value;
							}
						}

						if (databaseId != null)
						{
							var result = ts.GetClientServerGroups(databaseId.Value);
							if (result.Ok)
								serverGroups = result.Value;
						}
					}

					execCtx.ChannelGroupId = channelGroup;
					execCtx.ServerGroups = serverGroups ?? Array.Empty<ServerGroupId>();

					if (needsPermOverview.Length > 0 && databaseId != null && channelId != null)
					{
						// TODO check if there is any better way to only get the permissions needed.
						var result = tsClient.PermOverview(databaseId.Value, channelId.Value, 0);
						if (result.Ok)
						{
							execCtx.Permissions = new PermOverview[Enum.GetValues(typeof(TsPermission)).Length];
							foreach (var perm in result.Value)
							{
								if (perm.PermissionId < 0 || (int)perm.PermissionId >= execCtx.Permissions.Length)
									continue;
								var cur = execCtx.Permissions[(int)perm.PermissionId];
								execCtx.Permissions[(int)perm.PermissionId] = cur == null ? perm : cur.Combine(perm);
							}
						}
					}
				}
			}
			else if (info.TryGet<ApiCall>(out var apiCallData))
			{
				execCtx.ClientUid = apiCallData.ClientUid;
				execCtx.ApiToken = apiCallData.Token;
				execCtx.ApiCallerIp = apiCallData.IpAddress;
				execCtx.IsApi = true;
			}

			if (info.TryGet<Bot>(out var bot))
			{
				var botInfo = bot.GetInfo();
				execCtx.Bot = botInfo.Name;
				execCtx.Host = botInfo.Server;
			}

			if (localRootRule != null)
				ProcessNode(localRootRule, execCtx);

			if (execCtx.MatchingRules.Count == 0)
				return execCtx;

			foreach (var rule in execCtx.MatchingRules)
				execCtx.DeclAdd.UnionWith(rule.DeclAdd);

			info.AddModule(execCtx);

			return execCtx;
		}

		private RightsRule TryGetRootSafe()
		{
			var localRootRule = rootRule;
			if (localRootRule != null && !needsRecalculation)
				return localRootRule;

			lock (rootRuleLock)
			{
				if (rootRule != null && !needsRecalculation)
					return rootRule;

				rootRule = ReadFile();
				return rootRule;
			}
		}

		private static bool ProcessNode(RightsRule rule, ExecuteContext ctx)
		{
			// check if node matches
			if (rule.Matches(ctx))
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

		public bool Reload()
		{
			needsRecalculation = true;
			return TryGetRootSafe() != null;
		}

		// Loading and Parsing

		private RightsRule ReadFile()
		{
			try
			{
				CreateDefaultConfigIfNotExists();

				var table = Toml.ReadFile(config.Path);
				var ctx = new ParseContext(registeredRights);
				RecalculateRights(table, ctx);
				foreach (var err in ctx.Errors)
					Log.Error(err);
				foreach (var warn in ctx.Warnings)
					Log.Warn(warn);

				if (ctx.Errors.Count == 0)
				{
					needsAvailableChanGroups = ctx.NeedsAvailableChanGroups;
					needsAvailableGroups = ctx.NeedsAvailableGroups;
					needsPermOverview = ctx.NeedsPermOverview;
					needsRecalculation = false;
					return ctx.RootRule;
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, "The rights file could not be parsed");
			}
			return null;
		}

		public void CreateDefaultConfigIfNotExists()
		{
			CreateConfig(new CreateFileSettings { OverwriteIfExists = false });
		}

		public void CreateConfig(CreateFileSettings settings)
		{
			if (!settings.OverwriteIfExists && File.Exists(config.Path))
				return;

			Log.Info("Creating new permission file ({@settings})", settings);

			string toml = null;
			using (var fs = Util.GetEmbeddedFile("TS3AudioBot.Rights.DefaultRights.toml"))
			using (var reader = new StreamReader(fs, Tools.Utf8Encoder))
			{
				toml = reader.ReadToEnd();
			}

			using (var fs = File.Open(config.Path, FileMode.Create, FileAccess.Write, FileShare.None))
			using (var writer = new StreamWriter(fs, Tools.Utf8Encoder))
			{
				string replaceAdminUids = settings.AdminUids != null
					? string.Join(" ,", settings.AdminUids.Select(x => $"\"{x}\""))
					: string.Empty;
				toml = toml.Replace("\"_admin_uid_\"", replaceAdminUids);

				writer.Write(toml);
			}
		}

		public void CreateConfigIfNotExists(bool interactive = false)
		{
			if (File.Exists(config.Path))
				return;

			Log.Warn("No permission file found.");

			var settings = new CreateFileSettings
			{
				OverwriteIfExists = false,
			};

			if (interactive)
			{
				Console.WriteLine("Do you want to set up an admin in the default permission file template? [Y/n]");
				if (Interactive.UserAgree(defaultTo: true))
				{
					var adminUid = Interactive.LoopAction("Please enter an admin uid", uid =>
					{
						if (!Uid.IsValid(uid))
						{
							Console.WriteLine("The uid seems to be invalid, continue anyway? [y/N]");
							return Interactive.UserAgree(defaultTo: false);
						}
						return true;
					});
					if (adminUid is null)
						return;

					settings.AdminUids = new[] { adminUid };
				}
			}

			CreateConfig(settings);
		}

		private static void RecalculateRights(TomlTable table, ParseContext parseCtx)
		{
			if (!parseCtx.RootRule.ParseChilden(table, parseCtx))
				return;

			parseCtx.SplitDeclarations();

			if (!ValidateUniqueGroupNames(parseCtx))
				return;

			if (!ResolveIncludes(parseCtx))
				return;

			if (!CheckCyclicGroupDependencies(parseCtx))
				return;

			BuildLevel(parseCtx.RootRule);

			LintDeclarations(parseCtx);

			NormalizeRules(parseCtx);

			FlattenGroups(parseCtx);

			FlattenRules(parseCtx.RootRule);

			CheckRequiredCalls(parseCtx);
		}

		private static HashSet<string> ExpandRights(IEnumerable<string> rights, ICollection<string> registeredRights)
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
		private static void NormalizeRules(ParseContext ctx)
		{
			foreach (var rule in ctx.Rules)
			{
				var denyNormalized = ExpandRights(rule.DeclDeny, ctx.RegisteredRights);
				rule.DeclDeny = denyNormalized.ToArray();
				var addNormalized = ExpandRights(rule.DeclAdd, ctx.RegisteredRights);
				addNormalized.ExceptWith(rule.DeclDeny);
				rule.DeclAdd = addNormalized.ToArray();

				var undeclared = rule.DeclAdd.Except(ctx.RegisteredRights)
					.Concat(rule.DeclDeny.Except(ctx.RegisteredRights));
				foreach (var right in undeclared)
				{
					ctx.Warnings.Add($"Right \"{right}\" is not registered.");
				}
			}
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
			{
				foreach (var child in rootRule.Children)
				{
					BuildLevel(child, level + RuleLevelSize);
				}
			}
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
			var root = ctx.Rules.First(x => x.Parent is null);
			if (root.Includes.Length == 0 && root.DeclDeny.Length > 0)
				ctx.Warnings.Add("Root rule \"-\" declaration has no effect");

			// check if rule has no matcher
			foreach (var rule in ctx.Rules)
			{
				if (!rule.HasMatcher() && rule.Parent != null)
					ctx.Warnings.Add("Rule has no matcher and will always match");
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
		/// Sums up all includes for each group and includes them directly into the
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
		/// Sums up all includes and parent rule declarations for each rule and includes them
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

		/// <summary>
		/// Checks which ts3client calls need to made to get all information
		/// for the required matcher.
		/// </summary>
		/// <param name="ctx">The parsing context for the current file processing.</param>
		private static void CheckRequiredCalls(ParseContext ctx)
		{
			var needsPermOverview = new HashSet<TsPermission>();

			foreach (var group in ctx.Rules)
			{
				foreach (var matcher in group.Matcher)
				{
					switch (matcher)
					{
					case MatchServerGroupId sgid:
						ctx.NeedsAvailableGroups = true;
						break;

					case MatchChannelGroupId sgid:
						ctx.NeedsAvailableChanGroups = true;
						break;

					case MatchPermission sgid:
						needsPermOverview.UnionWith(sgid.ComparingPermissions());
						break;
					}
				}
			}
			ctx.NeedsPermOverview = needsPermOverview.Count > 0 ? needsPermOverview.ToArray() : Array.Empty<TsPermission>();
		}
	}
}
