// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem.Ast
{
	using System.Text;

	internal class AstValue : AstNode
	{
		public override AstType Type => AstType.Value;
		public string Value { get; set; }

		public void BuildValue()
		{
			Value = FullRequest.Substring(Position, Length);
		}

		public override void Write(StringBuilder strb, int depth) => strb.Space(depth).Append(Value);
	}
}
