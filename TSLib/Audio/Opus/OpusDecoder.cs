// Copyright 2012 John Carruthers
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;

namespace TSLib.Audio.Opus
{
	/// <summary>
	/// Opus audio decoder.
	/// </summary>
	public sealed class OpusDecoder : IDisposable
	{
		/// <summary>
		/// Creates a new Opus decoder.
		/// </summary>
		/// <param name="outputSampleRate">Sample rate to decode at (Hz). This must be one of 8000, 12000, 16000, 24000, or 48000.</param>
		/// <param name="outputChannels">Number of channels to decode.</param>
		/// <returns>A new <c>OpusDecoder</c>.</returns>
		public static OpusDecoder Create(int outputSampleRate, int outputChannels)
		{
			if (outputSampleRate != 8000 &&
				outputSampleRate != 12000 &&
				outputSampleRate != 16000 &&
				outputSampleRate != 24000 &&
				outputSampleRate != 48000)
				throw new ArgumentOutOfRangeException(nameof(outputSampleRate));
			if (outputChannels != 1 && outputChannels != 2)
				throw new ArgumentOutOfRangeException(nameof(outputChannels));

			IntPtr decoder = NativeMethods.opus_decoder_create(outputSampleRate, outputChannels, out IntPtr error);
			if ((Errors)error != Errors.Ok)
			{
				throw new Exception("Exception occured while creating decoder");
			}
			return new OpusDecoder(decoder, outputSampleRate, outputChannels);
		}

		private IntPtr decoder;

		private OpusDecoder(IntPtr decoder, int outputSamplingRate, int outputChannels)
		{
			this.decoder = decoder;
			OutputSamplingRate = outputSamplingRate;
			OutputChannels = outputChannels;
		}

		/// <summary>
		/// Produces PCM samples from Opus encoded data.
		/// </summary>
		/// <param name="inputOpusData">Opus encoded data to decode, null for dropped packet.</param>
		/// <param name="dataLength">Length of data to decode.</param>
		/// <param name="outputDecodedBuffer">PCM audio samples buffer.</param>
		/// <returns>PCM audio samples.</returns>
		public Span<byte> Decode(Span<byte> inputOpusData, Span<byte> outputDecodedBuffer)
		{
			if (disposed)
				throw new ObjectDisposedException("OpusDecoder");

			if (inputOpusData.Length == 0)
				return Span<byte>.Empty;

			int frameSize = FrameCount(outputDecodedBuffer.Length);

			// TODO fix hacky ref implementation once there is a good alternative with spans
			int length = NativeMethods.opus_decode(decoder, ref inputOpusData[0], inputOpusData.Length, ref outputDecodedBuffer[0], frameSize, 0);

			if (length < 0)
				throw new Exception("Decoding failed - " + (Errors)length);

			// TODO implement forward error corrected packet
			//else
			//	length = NativeMethods.opus_decode(decoder, null, 0, decoded, frameCount, (ForwardErrorCorrection) ? 1 : 0);

			var decodedLength = length * 2 * OutputChannels;

			return outputDecodedBuffer.Slice(0, decodedLength);
		}

		/// <summary>
		/// Determines the number of frames that can fit into a buffer of the given size.
		/// </summary>
		/// <param name="bufferSize"></param>
		/// <returns></returns>
		public int FrameCount(int bufferSize)
		{
			//  seems like bitrate should be required
			const int bitrate = 16;
			int bytesPerSample = (bitrate / 8) * OutputChannels;
			return bufferSize / bytesPerSample;
		}

		/// <summary>
		/// Gets the output sampling rate of the decoder.
		/// </summary>
		public int OutputSamplingRate { get; private set; }

		/// <summary>
		/// Gets the number of channels of the decoder.
		/// </summary>
		public int OutputChannels { get; private set; }

		/// <summary>
		/// Gets or sets whether forward error correction is enabled or not.
		/// </summary>
		public bool ForwardErrorCorrection { get; set; }

		~OpusDecoder()
		{
			Dispose();
		}

		private bool disposed;
		public void Dispose()
		{
			if (disposed)
				return;

			GC.SuppressFinalize(this);

			if (decoder != IntPtr.Zero)
			{
				NativeMethods.opus_decoder_destroy(decoder);
				decoder = IntPtr.Zero;
			}

			disposed = true;
		}
	}
}
