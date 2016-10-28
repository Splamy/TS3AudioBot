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
	using CSCore;
	using Helper;
	using Opus;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3Client;

	class AudioEncoder : IDisposable
	{
		public Codec Codec { get; }
		public int SampleRate { get; }
		public int Channel { get; }
		public int BitsPerSample { get; }

		public int OptimalPacketSize => bytesPerSegment;

		// opus
		OpusEncoder opusEncoder;

		private const int segmentFrames = 960;
		private int bytesPerSegment;
		private byte[] _notEncodedBuffer = new byte[0];
		private Queue<Tuple<byte[], int>> opusQueue;

		public AudioEncoder(Codec codec)
		{
			Util.Init(ref opusQueue);
			Codec = codec;

			switch (codec)
			{
				case Codec.SpeexNarrowband:
					throw new NotSupportedException();
				case Codec.SpeexWideband:
					throw new NotSupportedException();
				case Codec.SpeexUltraWideband:
					throw new NotSupportedException();
				case Codec.CeltMono:
					throw new NotSupportedException();

				case Codec.OpusVoice:
					SampleRate = 48000;
					Channel = 1;
					opusEncoder = OpusEncoder.Create(SampleRate, Channel, Application.Voip);
					opusEncoder.Bitrate = 8192 * 2;
					break;

				case Codec.OpusMusic:
					SampleRate = 48000;
					Channel = 2;
					opusEncoder = OpusEncoder.Create(SampleRate, Channel, Application.Audio);
					opusEncoder.Bitrate = 8192 * 4;
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(codec));
			}

			BitsPerSample = 16;
			bytesPerSegment = opusEncoder.FrameByteCount(segmentFrames);
		}

		public void PushPCMAudio(byte[] buffer, int bufferlen)
		{
			byte[] soundBuffer = new byte[bufferlen + _notEncodedBuffer.Length];
			Array.Copy(_notEncodedBuffer, 0, soundBuffer, 0, _notEncodedBuffer.Length);
			Array.Copy(buffer, 0, soundBuffer, _notEncodedBuffer.Length, bufferlen);

			int byteCap = bytesPerSegment;
			int segmentCount = (int)Math.Floor((decimal)soundBuffer.Length / byteCap);
			int segmentsEnd = segmentCount * byteCap;
			int notEncodedCount = soundBuffer.Length - segmentsEnd;
			_notEncodedBuffer = new byte[notEncodedCount];
			Array.Copy(soundBuffer, segmentsEnd, _notEncodedBuffer, 0, notEncodedCount);

			for (int i = 0; i < segmentCount; i++)
			{
				byte[] segment = new byte[byteCap];
				for (int j = 0; j < segment.Length; j++)
					segment[j] = soundBuffer[(i * byteCap) + j];
				int len;
				byte[] buff = opusEncoder.Encode(segment, segment.Length, out len);
				opusQueue.Enqueue(new Tuple<byte[], int>(buff, len));
			}
		}

		public Tuple<byte[], int> GetPacket()
		{
			if (opusQueue.Any())
				return opusQueue.Dequeue();
			else
				return null;
		}

		public static TimeSpan? GetPlayLength(IWaveSource source)
		{
			var len = source.Length;
			if (len == 0) return null;
			var format = source.WaveFormat;
			return TimeSpan.FromSeconds(len / (double)(format.SampleRate * (format.BitsPerSample / 8) * format.Channels));
		}

		public void Clear()
		{
			opusQueue.Clear();
		}

		public void Dispose()
		{
			opusEncoder?.Dispose();
		}
	}
}
