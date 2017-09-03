// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Audio
{
	using Helper;
	using Opus;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3Client;

	// NOT Thread-Safe
	internal class AudioEncoder : IDisposable
	{
		public Codec Codec { get; }
		public int SampleRate { get; }
		public int Channels { get; }
		public int BitsPerSample { get; }

		public int OptimalPacketSize { get; }
		public int Bitrate { get => opusEncoder.Bitrate; set => opusEncoder.Bitrate = value; }

		public bool HasPacket => opusQueue.Count > 0;

		// opus
		private OpusEncoder opusEncoder;

		private const int SegmentFrames = 960;
		private byte[] soundBuffer = new byte[0];
		private int soundBufferLength = 0;
		private byte[] notEncodedBuffer = new byte[0];
		private int notEncodedBufferLength = 0;
		private byte[] segment = null;
		private Queue<PartialArray> opusQueue;
		private Queue<byte[]> freeArrays;

		public AudioEncoder(Codec codec)
		{
			Util.Init(ref opusQueue);
			Util.Init(ref freeArrays);
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

		private byte[] GetFreeArray()
		{
			if (freeArrays.Count > 0)
				return freeArrays.Dequeue();
			else
				return new byte[opusEncoder.MaxDataBytes];
		}

		public void PushPCMAudio(byte[] buffer, int bufferlen)
		{
			int newSoundBufferLength = bufferlen + notEncodedBufferLength;
			if (newSoundBufferLength > soundBuffer.Length)
				soundBuffer = new byte[newSoundBufferLength];
			soundBufferLength = newSoundBufferLength;

			Array.Copy(notEncodedBuffer, 0, soundBuffer, 0, notEncodedBufferLength);
			Array.Copy(buffer, 0, soundBuffer, notEncodedBufferLength, bufferlen);

			int byteCap = OptimalPacketSize;
			int segmentCount = (int)Math.Floor((float)soundBufferLength / byteCap);
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
				byte[] encodedBuffer = GetFreeArray();
				opusEncoder.Encode(segment, segment.Length, encodedBuffer, out int len);
				opusQueue.Enqueue(new PartialArray { Array = encodedBuffer, Length = len });
			}
		}

		public PartialArray GetPacket()
		{
			if (!HasPacket)
				throw new InvalidOperationException();
			return opusQueue.Dequeue();
		}

		public void ReturnPacket(byte[] packet)
		{
			freeArrays.Enqueue(packet);
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

	internal struct PartialArray
	{
		public byte[] Array;
		public int Length;
	}
}
