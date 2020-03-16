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
	public abstract class AstNode
	{
		public abstract AstType Type { get; }

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

	internal static class AstNodeExtensions
	{
		public const int SpacePerTab = 2;
		public static StringBuilder Space(this StringBuilder strb, int depth) => strb.Append(' ', depth * SpacePerTab);
	}
}
