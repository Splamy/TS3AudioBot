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
	using System.Diagnostics;

	internal class PreciseAudioTimer
	{
		public int SampleRate { get; }
		public int Channel { get; }
		public int BitsPerSample { get; }
		public int BytesPerSecond { get; }

		private long byteCnt;
		private readonly Stopwatch stopwatch;
		public TimeSpan BufferLength => CalcTime();

		public PreciseAudioTimer(int sampleRate, int bits, int channel)
		{
			if (bits != 8 && bits != 16 && bits != 24 && bits != 32) throw new ArgumentException(nameof(bits));
			if (channel != 1 && channel != 2) throw new ArgumentException(nameof(channel));
			stopwatch = new Stopwatch();

			SampleRate = sampleRate;
			BitsPerSample = bits;
			Channel = channel;
			BytesPerSecond = SampleRate * (BitsPerSample / 8) * Channel;
		}

		public void Start()
		{
			byteCnt = 0;
			stopwatch.Restart();
		}

		public void Stop()
		{
			stopwatch.Stop();
		}

		public void PushBytes(int count)
		{
			byteCnt += count;
		}

		private TimeSpan CalcTime()
		{
			long usedBytesPerSec = (stopwatch.ElapsedMilliseconds * BytesPerSecond) / 1000;
			long remainingBytes = byteCnt - usedBytesPerSec;
			return TimeSpan.FromSeconds(remainingBytes / (double)BytesPerSecond);
		}
	}
}
