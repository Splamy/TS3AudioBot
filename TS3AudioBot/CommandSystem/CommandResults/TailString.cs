// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem.CommandResults
{
	public class TailString : IWrappedResult
	{
		public string Content { get; }
		public string Tail { get; }
		object? IWrappedResult.Content => Content;

		public TailString(string contentArg, string tailArg)
		{
			Content = contentArg;
			Tail = tailArg;
		}

		public override string ToString() => Content;
	}
}
