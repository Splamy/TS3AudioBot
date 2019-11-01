// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Generic;
using System.Text;

namespace TS3AudioBot.CommandSystem.Ast
{
	internal class AstCommand : AstNode
	{
		public override AstType Type => AstType.Command;

		public List<AstNode> Parameter { get; } = new List<AstNode>();

		public override void Write(StringBuilder strb, int depth)
		{
			strb.Space(depth);
			if (Parameter.Count == 0)
			{
				strb.Append("<Invalid empty command>");
			}
			else
			{
				if (Parameter[0] is AstValue comName)
					strb.Append("!").Append(comName.Value);
				else
					strb.Append("<Invalid command name>");

				for (int i = 1; i < Parameter.Count; i++)
				{
					strb.AppendLine();
					Parameter[i].Write(strb, depth + 1);
				}
			}
		}
	}
}
