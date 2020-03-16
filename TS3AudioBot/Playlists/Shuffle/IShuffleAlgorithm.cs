// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Playlists.Shuffle
{
	public interface IShuffleAlgorithm
	{
		int Seed { get; set; }
		int Length { get; set; }
		int Index { get; set; }
		// Returns true if the step reached the end of the list and wrapped around
		bool Next();
		// Returns true if the step reached the end of the list and wrapped around
		bool Prev();
	}

	// Output conventions:
	//
	// if Index = x, x >= Length
	//   => Index = Tools.MathMod(Index, Length)
	// if Index = x, x < 0
	//   => Index : undefined
	// if Index = x, Length < 0
	//   => Index = -1
}
