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
	using RuleRet = System.Collections.Generic.IList<Token>;

	public class Context
	{
		public string Input { get; }
		public int Position { get; set; }
		public int HighestPosition { get; set; }

		public string CurrentString => Input.Substring(Position);
		public bool HasChar => Position < Input.Length;
		public char CurrentChar => Input[Position];

		public Context(string input)
		{
			Input = input;
			Position = 0;
		}

		public RuleRet Take(int i)
		{
			var token = new Token(this, Position, i, null);
			Position += i;
			HighestPosition = Math.Max(HighestPosition, Position);
			return new Token[] { token };
		}
	}
}
