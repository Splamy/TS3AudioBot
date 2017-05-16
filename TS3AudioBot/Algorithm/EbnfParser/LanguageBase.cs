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

namespace TS3AudioBot.Algorithm.EbnfParser
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Rule = System.Func<Context, System.Collections.Generic.IList<Token>>;
	using RuleRet = System.Collections.Generic.IList<Token>;

	public static class LanguageBase
	{
		// [Lexer] transparent functions:
		// fn: Rule -> Rule
		// [Parser] named fuction
		// fn: Context -> RuleRet

		private static RuleRet Empty(Context ctx) => ctx.Take(0);

		public static Rule And(params Rule[] matcher)
		{
			return new Rule((ctx) =>
			{
				if (matcher == null)
					return null;
				var children = new List<Token>();
				int position = ctx.Position;
				for (int i = 0; i < matcher.Length; i++)
				{
					var tokens = matcher[i].Invoke(ctx);
					if (tokens == null)
					{
						ctx.Position = position;
						return null;
					}
					children.AddRange(tokens);
				}
				return children;
			});
		}

		public static Rule Or(params Rule[] matcher)
		{
			return new Rule((ctx) =>
			{
				if (matcher == null)
					return null;
				int position = ctx.Position;
				for (int i = 0; i < matcher.Length; i++)
				{
					ctx.Position = position;
					var token = matcher[i].Invoke(ctx);
					if (token != null)
						return token;
				}
				ctx.Position = position;
				return null;
			});
		}

		public static Rule ZeroOrOne(Rule matcher)
		{
			return new Rule((ctx) =>
			{
				if (matcher == null)
					return null;
				var token = matcher.Invoke(ctx);
				if (token != null)
					return token;
				else
					return ctx.Take(0);
			});
		}

		public static Rule OneOrMany(Rule matcher)
		{
			return new Rule((ctx) =>
			{
				if (matcher == null)
					return null;
				int position = ctx.Position;
				var children = new List<Token>();
				RuleRet tokens;
				while ((tokens = matcher.Invoke(ctx)) != null)
					children.AddRange(tokens);
				if (children.Count == 0)
					return null;
				else
					return children;
			});
		}

		public static Rule ZeroOrMany(Rule matcher)
		{
			return new Rule((ctx) =>
			{
				if (matcher == null)
					return null;
				int position = ctx.Position;
				var children = new List<Token>();
				RuleRet tokens;
				while ((tokens = matcher.Invoke(ctx)) != null)
					children.AddRange(tokens);
				return children;
			});
		}

		public static Rule ExactlyTimes(Rule matcher, int rep)
		{
			if (rep == 0)
				return Empty;
			else if (rep == 1)
				return matcher;
			else
				return BetweenTimes(matcher, rep, rep);
		}

		public static Rule BetweenTimes(Rule matcher, int min, int max)
		{
			if (min < 0)
				throw new ArgumentOutOfRangeException(nameof(min));
			if (min > max)
				throw new ArgumentOutOfRangeException(nameof(min));

			if (min == 0 && max == 0)
				return Empty;
			else if (min == 0 && max == 1)
				return ZeroOrOne(matcher);
			else if (min == 1 && max == 1)
				return matcher;

			return new Rule((ctx) =>
			{
				if (matcher == null)
					return null;
				int position = ctx.Position;
				var children = new List<Token>();

				for (int i = 0; i < max; i++)
				{
					var tokens = matcher.Invoke(ctx);
					if (tokens == null)
						break;
					children.AddRange(tokens);
				}
				if (children.Count < min)
				{
					ctx.Position = position;
					return null;
				}
				return children;
			});
		}

		public static Rule Const(string value)
		{
			return new Rule(ctx =>
			{
				if (ctx.CurrentString.StartsWith(value))
					return ctx.Take(value.Length);
				else
					return null;
			});
		}

		public static RuleRet ToToken(this Rule rule, Context ctx, string name)
		{
			var pos = ctx.Position;
			var result = rule.Invoke(ctx);
			if (result == null)
				return null;
			return new Token[] { new Token(ctx, pos, ctx.Position - pos, result.ToArray()).SetName(name) };
		}
	}
}
