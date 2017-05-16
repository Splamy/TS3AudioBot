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
	using System.Text;

	public class Token
	{
		public Context Context { get; }

		public string Name { get; set; }
		public int Position { get; }
		public int MatchLength { get; }
		public string MatchString => Context.Input.Substring(Position, MatchLength);
		public Token[] Children { get; }
		public bool IsLeaf => Children == null;

		public Token(Context ctx, int pos, int len, Token[] children)
		{
			Context = ctx;
			Position = pos;
			MatchLength = len;
			Children = children;
		}

		public Token SetName(string name) { Name = name; return this; }

		public override string ToString() => $"<{Name ?? ""}> : {Position:0000} : \"{MatchString}\"";

		public void BuildTree(StringBuilder strb, int depth)
		{
			strb.AppendLine().Append(' ', depth * 2).AppendFormat("<{0}> [{1:0000},{2}] ", Name ?? "", Position, MatchLength);
			if (IsLeaf)
				strb.Append(MatchString);

			if (!IsLeaf)
				foreach (var child in Children)
				{
					if (child.Name == null)
						strb.Append(child.MatchString);
					else
						child.BuildTree(strb, depth + 1);
				}
		}
	}
}
