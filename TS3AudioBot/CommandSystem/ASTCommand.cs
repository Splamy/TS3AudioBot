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
	using System.Collections.Generic;
	using System.Text;

	internal class ASTCommand : ASTNode
	{
		public override ASTType Type => ASTType.Command;

		public List<ASTNode> Parameter { get; set; }

		public ASTCommand()
		{
			Parameter = new List<ASTNode>();
		}

		public override void Write(StringBuilder strb, int depth)
		{
			strb.Space(depth);
			if (Parameter.Count == 0)
			{
				strb.Append("<Invalid empty command>");
			}
			else
			{
				ASTValue comName = Parameter[0] as ASTValue;
				if (comName == null)
					strb.Append("<Invalid command name>");
				else
					strb.Append("!").Append(comName.Value);

				for (int i = 1; i < Parameter.Count; i++)
				{
					strb.AppendLine();
					Parameter[i].Write(strb, depth + 1);
				}
			}
		}
	}
}
