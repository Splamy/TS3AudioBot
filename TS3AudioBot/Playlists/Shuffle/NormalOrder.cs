// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using TSLib.Helper;

namespace TS3AudioBot.Playlists.Shuffle
{
	public class NormalOrder : IShuffleAlgorithm
	{
		public int Seed { get; set; }
		public int Length { get; set; }
		public int Index { get; set; }

		public bool Next()
		{
			Index = Tools.MathMod(Index + 1, Length);
			return Index == 0;
		}

		public bool Prev()
		{
			Index = Tools.MathMod(Index - 1, Length);
			return Index == Length - 1;
		}
	}
}
