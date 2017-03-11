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

namespace TS3AudioBot.CommandSystem
{
	using System.Text;

	internal abstract class ASTNode
	{
		public abstract ASTType Type { get; }

		public string FullRequest { get; set; }
		public int Position { get; set; }
		public int Length { get; set; }

		public abstract void Write(StringBuilder strb, int depth);
		public sealed override string ToString()
		{
			var strb = new StringBuilder();
			Write(strb, 0);
			return strb.ToString();
		}
	}

	internal static class ASTNodeExtensions
	{
		public const int SpacePerTab = 2;
		public static StringBuilder Space(this StringBuilder strb, int depth) => strb.Append(' ', depth * SpacePerTab);
	}
}
