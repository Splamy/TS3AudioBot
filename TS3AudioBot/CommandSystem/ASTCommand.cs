// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

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
