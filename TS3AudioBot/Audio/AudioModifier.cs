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

namespace TS3AudioBot.Audio
{
	using System;

	static class AudioModifier
	{
		public static void AdjustVolume(byte[] audioSamples, int length, float volume)
		{
			for (int i = 0; i < length; i += 2)
			{
				var res = (short)(BitConverter.ToInt16(audioSamples, i) * volume);
				var bt = BitConverter.GetBytes(res);
				audioSamples[i] = bt[0];
				audioSamples[i + 1] = bt[1];
			}
		}
	}
}
