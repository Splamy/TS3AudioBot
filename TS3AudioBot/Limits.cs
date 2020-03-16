// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot
{
	public static class Limits
	{
		/// <summary>Max stream size to download before aborting.</summary>
		public static long MaxImageStreamSize { get; } = 10_000_000;
		/// <summary>Max image size which is allowed to be resized from.</summary>
		public static long MaxImageDimension { get; } = 10_000;
	}
}
