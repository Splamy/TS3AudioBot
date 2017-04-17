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
	using Helper;
	using Opus;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3Client;

	internal class AudioEncoder : IDisposable
	{
		public Codec Codec { get; }
		public int SampleRate { get; }
		public int Channels { get; }
		public int BitsPerSample { get; }

		public int OptimalPacketSize { get; }
		public int Bitrate { get { return opusEncoder.Bitrate; } set { opusEncoder.Bitrate = value; } }

		public bool HasPacket => opusQueue.Any();

		// opus
		OpusEncoder opusEncoder;

		private const int SegmentFrames = 960;
		private byte[] soundBuffer = new byte[0];
		private int soundBufferLength = 0;
		private byte[] notEncodedBuffer = new byte[0];
		private int notEncodedBufferLength = 0;
		private byte[] segment = null;
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
				Channels = 1;
				opusEncoder = OpusEncoder.Create(SampleRate, Channels, Application.Voip);
				Bitrate = 8192 * 2;
				break;

			case Codec.OpusMusic:
				SampleRate = 48000;
				Channels = 2;
				opusEncoder = OpusEncoder.Create(SampleRate, Channels, Application.Audio);
				Bitrate = 8192 * 4;
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(codec));
			}

			BitsPerSample = 16;
			OptimalPacketSize = opusEncoder.FrameByteCount(SegmentFrames);
			segment = new byte[OptimalPacketSize];
		}

		public void PushPCMAudio(byte[] buffer, int bufferlen)
		{
			int newSoundBufferLength = bufferlen + notEncodedBuffer.Length;
			if (newSoundBufferLength > soundBuffer.Length)
				soundBuffer = new byte[newSoundBufferLength]; // TODO optimize not encoded buffer
			soundBufferLength = newSoundBufferLength;

			Array.Copy(notEncodedBuffer, 0, soundBuffer, 0, notEncodedBuffer.Length);
			Array.Copy(buffer, 0, soundBuffer, notEncodedBuffer.Length, bufferlen);

			int byteCap = OptimalPacketSize;
			int segmentCount = (int)Math.Floor((decimal)soundBuffer.Length / byteCap);
			int segmentsEnd = segmentCount * byteCap;
			int newNotEncodedBufferLength = soundBufferLength - segmentsEnd;
			if (newNotEncodedBufferLength > notEncodedBuffer.Length)
				notEncodedBuffer = new byte[newNotEncodedBufferLength];
			notEncodedBufferLength = newNotEncodedBufferLength;
			Array.Copy(soundBuffer, segmentsEnd, notEncodedBuffer, 0, notEncodedBufferLength);

			for (int i = 0; i < segmentCount; i++)
			{
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

		public TimeSpan GetPlayLength(int bytes)
		{
			return TimeSpan.FromSeconds(bytes / (double)(SampleRate * (BitsPerSample / 8) * Channels));
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
