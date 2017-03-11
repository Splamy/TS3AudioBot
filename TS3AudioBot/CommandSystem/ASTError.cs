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

	internal class ASTError : ASTNode
	{
		public override ASTType Type => ASTType.Error;

		public string Description { get; }

		public ASTError(ASTNode referenceNode, string description)
		{
			FullRequest = referenceNode.FullRequest;
			Position = referenceNode.Position;
			Length = referenceNode.Length;
			Description = description;
		}

		public ASTError(string request, int pos, int len, string description)
		{
			FullRequest = request;
			Position = pos;
			Length = len;
			Description = description;
		}

		public override void Write(StringBuilder strb, int depth)
		{
			strb.AppendLine(FullRequest);
			if (Position == 1) strb.Append('.');
			else if (Position > 1) strb.Append(' ', Position);
			strb.Append('~', Length).Append('^').AppendLine();
			strb.Append("Error: ").AppendLine(Description);
		}
	}
}
