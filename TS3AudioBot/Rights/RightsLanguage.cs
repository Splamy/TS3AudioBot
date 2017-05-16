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
	using TS3AudioBot.Algorithm.EbnfParser;
	using static TS3AudioBot.Algorithm.EbnfParser.LanguageBase;
	using Rule = System.Func<Algorithm.EbnfParser.Context, System.Collections.Generic.IList<Algorithm.EbnfParser.Token>>;
	using RuleRet = System.Collections.Generic.IList<Algorithm.EbnfParser.Token>;

	public static class RightsLanguage
	{
		public static Rule HelperSharp(Rule matcher)
			=> And(
				ZeroOrMany(RuleSep),
				ZeroOrOne(matcher),
				ZeroOrMany(And(OneOrMany(RuleSep), matcher)),
				ZeroOrMany(RuleSep)
			);

		public static RuleRet RuleSyntax(Context ctx) => HelperSharp(RuleLvl3right).ToToken(ctx, "Syntax");

		public static RuleRet RuleLvl3right(Context ctx) => Or(
			And(RuleLvl3Identifier, ZeroOrMany(RuleSep), Const("{"), HelperSharp(RuleLvl2right), Const("}")),
			And(RuleLvl3Identifier, Const("::"), RuleLvl2right),
			RuleLvl2right).ToToken(ctx, "lvl3right-decl");
		public static RuleRet RuleLvl2right(Context ctx) => Or(
			And(RuleLvl2Identifier, ZeroOrMany(RuleSep), Const("{"), HelperSharp(RuleLvl1right), Const("}")),
			And(RuleLvl2Identifier, Const("::"), RuleLvl1right),
			RuleLvl1right).ToToken(ctx, "lvl2right-decl");
		public static RuleRet RuleLvl1right(Context ctx) => Or(
			And(RuleLvl1Identifier, ZeroOrMany(RuleSep), Const("{"), HelperSharp(RuleLvl0right), Const("}")),
			And(RuleLvl1Identifier, Const("::"), RuleLvl0right),
			RuleLvl0right).ToToken(ctx, "lvl1right-decl");
		public static RuleRet RuleLvl0right(Context ctx)
			=> And(Or(Const("+"), Const("-")), Or(RuleCmdpath, RuleAlias)).ToToken(ctx, "lvl0right-decl");

		public static RuleRet RuleLvl3Identifier(Context ctx) => Or(RuleHost, RuleServer).ToToken(ctx, "lvl3Identifier");
		public static RuleRet RuleLvl2Identifier(Context ctx) => Or(RuleGroup, RuleClient).ToToken(ctx, "lvl2Identifier");
		public static RuleRet RuleLvl1Identifier(Context ctx) => Or(RuleAlias).ToToken(ctx, "lvl1Identifier");

		public static RuleRet RuleHost(Context ctx) => And(Const("host("), Or(RuleDomain, RuleIpaddress), ZeroOrOne(And(Const(":"), RuleNumber)), Const(")")).ToToken(ctx, "server");
		public static RuleRet RuleServer(Context ctx) => And(Const("server("), RuleBase64, Const(")")).ToToken(ctx, "server");
		public static RuleRet RuleGroup(Context ctx) => And(Const("group("), RuleNumber, Const(")")).ToToken(ctx, "group");
		public static RuleRet RuleClient(Context ctx) => And(Const("user("), RuleBase64, Const(")")).ToToken(ctx, "client");
		public static RuleRet RuleAlias(Context ctx) => And(Const("$"), RuleWord).ToToken(ctx, "alias");
		public static RuleRet RuleDomain(Context ctx) => OneOrMany(Or(RuleAlphanum, Const("-"), Const("."))).ToToken(ctx, "domain");
		public static RuleRet RuleIpaddress(Context ctx) => Or(RuleIpv4address, RuleIpv6address).ToToken(ctx, "ipaddress");
		public static RuleRet RuleIpv4address(Context ctx) => And(RuleNumber, ExactlyTimes(And(Const("."), RuleNumber), 3)).ToToken(ctx, "ipv4address");
		public static RuleRet RuleIpv6address(Context ctx)
			=> Or(
				OneOrMany(Or(RuleNumber, Const(":"))), And(Const("["),
				OneOrMany(Or(RuleNumber, Const(":"))), Const("]"))
			).ToToken(ctx, "ipv6address");
		public static RuleRet RuleBase64(Context ctx) => And(OneOrMany(Or(RuleAlphanum, Const("/"), Const("+"))), BetweenTimes(Const("="), 0, 2)).ToToken(ctx, "base64");
		public static RuleRet RuleNumber(Context ctx) => OneOrMany(RuleNumber_Helper).ToToken(ctx, "number");
		public static RuleRet RuleNumber_Helper(Context ctx)
			=> ctx.HasChar && (ctx.CurrentChar >= '0' && ctx.CurrentChar <= '9') ? ctx.Take(1) : null;

		public static RuleRet RuleCmdpath(Context ctx)
			=> Or(
				And(
					Const("!"),
					RuleWord,
					ZeroOrMany(And(Const(" "), RuleWord)),
					ZeroOrOne(Const(" *"))
				),
				Const("!*")
			).ToToken(ctx, "cmdpath");

		public static RuleRet RuleWord(Context ctx) => OneOrMany(RuleWord_Helper).ToToken(ctx, "word");
		public static RuleRet RuleWord_Helper(Context ctx)
			=> ctx.HasChar && (ctx.CurrentChar >= 'a' && ctx.CurrentChar <= 'z') ? ctx.Take(1) : null;

		public static RuleRet RuleSep(Context ctx)
			=> ctx.HasChar &&
			(ctx.CurrentChar == ' ' || ctx.CurrentChar == '\t'
			|| ctx.CurrentChar == '\r' || ctx.CurrentChar == '\n'
			|| ctx.CurrentChar == ',') ? ctx.Take(1) : null;

		public static RuleRet RuleAlphanum(Context ctx) => OneOrMany(RuleAlphanum_Helper).ToToken(ctx, "alphanum");
		public static RuleRet RuleAlphanum_Helper(Context ctx)
			=> ctx.HasChar &&
			((ctx.CurrentChar >= 'a' && ctx.CurrentChar <= 'z')
			|| (ctx.CurrentChar >= 'A' && ctx.CurrentChar <= 'Z')
			|| (ctx.CurrentChar >= '0' && ctx.CurrentChar <= '9')) ? ctx.Take(1) : null;
	}
}
