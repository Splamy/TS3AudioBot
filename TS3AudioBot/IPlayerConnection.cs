// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot
{
	using System;
	using Helper;

	public interface IPlayerConnection : IDisposable
	{
		void Initialize();

		event EventHandler OnSongEnd;

		R<int> GetVolume();
		void SetVolume(int value);
		R<TimeSpan> GetPosition();
		void SetPosition(TimeSpan value);
		R<bool> IsRepeated();
		void SetRepeated(bool value);
		R<bool> IsPaused();
		void SetPaused(bool value);
		R<TimeSpan> GetLength();
		R<bool> IsPlaying();

		R AudioStart(string url);
		R AudioStop();
	}
}
