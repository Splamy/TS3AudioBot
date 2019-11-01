// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Text;

namespace TS3AudioBot.CommandSystem.Ast
{
	internal class AstValue : AstNode
	{
		private string value;
		private string tailString;

		public override AstType Type => AstType.Value;

		public StringType StringType { get; set; }

		public int TailLength { get; set; }

		public string Value
		{
			get => value = (value ?? FullRequest.Substring(Position, Length));
			set { this.value = value; tailString = value; }
		}

		public string TailString
		{
			get
			{
				if (tailString == null)
				{
					if (TailLength == 0)
						tailString = FullRequest.Substring(Position);
					else
						tailString = FullRequest.Substring(Position, TailLength);
				}
				return tailString;
			}
		}

		public override void Write(StringBuilder strb, int depth) => strb.Space(depth).Append(Value);
	}
}
