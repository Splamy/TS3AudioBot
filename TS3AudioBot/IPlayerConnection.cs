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
	using System;

	/// <summary>Slim interface to control the audio player.</summary>
	public interface IPlayerConnection : IDisposable
	{
		event EventHandler OnSongEnd;

		float Volume { get; set; }
		TimeSpan Position { get; set; }
		bool Repeated { get; set; }
		bool Paused { get; set; }
		TimeSpan Length { get; }
		bool Playing { get; }

		R AudioStart(string url);
		R AudioStop();
	}
}
