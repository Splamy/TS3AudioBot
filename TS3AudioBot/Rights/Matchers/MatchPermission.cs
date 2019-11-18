// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TS3AudioBot.Helper;
using TSLib;
using TSLib.Helper;

namespace TS3AudioBot.Rights.Matchers
{
	internal class MatchPermission : Matcher
	{
		private static readonly Regex expressionMatch = new Regex(@"(\w+)\s*(<|>|=|>=|<=|!=)\s*(-?\d+|true|false)", Util.DefaultRegexConfig);
		private readonly Dictionary<TsPermission, (PermCompare, int)> permissions;

		public MatchPermission(string[] permissions, ParseContext ctx)
		{
			this.permissions = new Dictionary<TsPermission, (PermCompare, int)>(permissions.Length);
			foreach (var expression in permissions)
			{
				var match = expressionMatch.Match(expression);
				if (!match.Success)
				{
					ctx.Errors.Add($"The expression \"{expression}\" is not in the valid form of '<permission><compare><value>'");
					continue;
				}

				var permission = match.Groups[1].Value;
				var compare = match.Groups[2].Value;
				var value = match.Groups[3].Value;

				if (!Enum.TryParse<TsPermission>(permission, out var permissionId))
				{
					ctx.Errors.Add($"The teamspeak permission \"{permission}\" was not found");
					continue;
				}

				PermCompare compareOp;
				switch (compare)
				{
				case "=": compareOp = PermCompare.Equal; break;
				case "!=": compareOp = PermCompare.NotEqual; break;
				case ">": compareOp = PermCompare.Greater; break;
				case ">=": compareOp = PermCompare.GreaterOrEqual; break;
				case "<": compareOp = PermCompare.Less; break;
				case "<=": compareOp = PermCompare.LessOrEqual; break;
				default: continue;
				}

				if ((value == "true" || value == "false") && !permission.StartsWith("b_"))
					ctx.Warnings.Add("Comparing an integer permission with boolean value.");

				int valueNum;
				if (value == "true")
					valueNum = 1;
				else if (value == "false")
					valueNum = 0;
				else if (!int.TryParse(value, out valueNum))
				{
					ctx.Errors.Add($"The permission compare value is not valid.");
					continue;
				}

				this.permissions.Add(permissionId, (compareOp, valueNum));
			}
		}

		public IReadOnlyCollection<TsPermission> ComparingPermissions() => permissions.Keys;

		public override bool Matches(ExecuteContext ctx)
		{
			if (ctx.Permissions == null)
				return false;

			foreach (var perm in ctx.Permissions)
			{
				if (perm == null)
					continue;
				var permission = perm.PermissionId;
				var value = perm.PermissionValue;
				if (permissions.TryGetValue(permission, out (PermCompare op, int value) compare))
				{
					switch (compare.op)
					{
					case PermCompare.Equal: if (value == compare.value) return true; break;
					case PermCompare.NotEqual: if (value != compare.value) return true; break;
					case PermCompare.Greater: if (value > compare.value) return true; break;
					case PermCompare.GreaterOrEqual: if (value >= compare.value) return true; break;
					case PermCompare.Less: if (value < compare.value) return true; break;
					case PermCompare.LessOrEqual: if (value <= compare.value) return true; break;
					default: throw Tools.UnhandledDefault(compare.op);
					}
				}
			}
			return false;
		}
	}
}
