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
