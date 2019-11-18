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
	/// Opus codec wrapper.
	/// </summary>
	public sealed class OpusEncoder : IDisposable
	{
		/// <summary>
		/// Creates a new Opus encoder.
		/// </summary>
		/// <param name="inputSamplingRate">Sampling rate of the input signal (Hz). This must be one of 8000, 12000, 16000, 24000, or 48000.</param>
		/// <param name="inputChannels">Number of channels (1 or 2) in input signal.</param>
		/// <param name="application">Coding mode.</param>
		/// <returns>A new <c>OpusEncoder</c></returns>
		public static OpusEncoder Create(int inputSamplingRate, int inputChannels, Application application)
		{
			if (inputSamplingRate != 8000 &&
				inputSamplingRate != 12000 &&
				inputSamplingRate != 16000 &&
				inputSamplingRate != 24000 &&
				inputSamplingRate != 48000)
				throw new ArgumentOutOfRangeException(nameof(inputSamplingRate));
			if (inputChannels != 1 && inputChannels != 2)
				throw new ArgumentOutOfRangeException(nameof(inputChannels));

			IntPtr encoder = NativeMethods.opus_encoder_create(inputSamplingRate, inputChannels, application, out IntPtr error);
			if ((Errors)error != Errors.Ok)
			{
				throw new Exception("Exception occured while creating encoder");
			}
			return new OpusEncoder(encoder, inputSamplingRate, inputChannels, application);
		}

		private IntPtr encoder;

		private OpusEncoder(IntPtr encoder, int inputSamplingRate, int inputChannels, Application application)
		{
			this.encoder = encoder;
			InputSamplingRate = inputSamplingRate;
			InputChannels = inputChannels;
			Application = application;
		}

		/// <summary>
		/// Produces Opus encoded audio from PCM samples.
		/// </summary>
		/// <param name="inputPcmSamples">PCM samples to encode.</param>
		/// <param name="sampleLength">How many bytes to encode.</param>
		/// <param name="outputEncodedBuffer">The encoded data is written to this buffer.</param>
		/// <returns>Opus encoded audio buffer.</returns>
		public Span<byte> Encode(Span<byte> inputPcmSamples, int sampleLength, byte[] outputEncodedBuffer)
		{
			if (disposed)
				throw new ObjectDisposedException("OpusEncoder");

			int frames = FrameCount(inputPcmSamples.Length);
			// TODO fix hacky ref implementation once there is a good alternative with spans
			int encodedLength = NativeMethods.opus_encode(encoder, ref inputPcmSamples[0], frames, outputEncodedBuffer, sampleLength);

			if (encodedLength < 0)
				throw new Exception("Encoding failed - " + (Errors)encodedLength);

			return outputEncodedBuffer.AsSpan(0, encodedLength);
		}

		/// <summary>
		/// Determines the number of frames in the PCM samples.
		/// </summary>
		/// <param name="bufferSize"></param>
		/// <returns></returns>
		public int FrameCount(int bufferSize)
		{
			//  seems like bitrate should be required
			const int bitrate = 16;
			int bytesPerSample = (bitrate / 8) * InputChannels;
			return bufferSize / bytesPerSample;
		}

		/// <summary>
		/// Helper method to determine how many bytes are required for encoding to work.
		/// </summary>
		/// <param name="frameCount">Target frame size.</param>
		/// <returns></returns>
		public int FrameByteCount(int frameCount)
		{
			const int bitrate = 16;
			int bytesPerSample = (bitrate / 8) * InputChannels;
			return frameCount * bytesPerSample;
		}

		/// <summary>
		/// Gets the input sampling rate of the encoder.
		/// </summary>
		public int InputSamplingRate { get; private set; }

		/// <summary>
		/// Gets the number of channels of the encoder.
		/// </summary>
		public int InputChannels { get; private set; }

		/// <summary>
		/// Gets the coding mode of the encoder.
		/// </summary>
		public Application Application { get; private set; }

		/// <summary>
		/// Gets or sets the bitrate setting of the encoding.
		/// </summary>
		public int Bitrate
		{
			get
			{
				if (disposed)
					throw new ObjectDisposedException("OpusEncoder");
				var ret = NativeMethods.opus_encoder_ctl(encoder, Ctl.GetBitrateRequest, out int bitrate);
				if (ret < 0)
					throw new Exception("Encoder error - " + ((Errors)ret).ToString());
				return bitrate;
			}
			set
			{
				if (disposed)
					throw new ObjectDisposedException("OpusEncoder");
				var ret = NativeMethods.opus_encoder_ctl(encoder, Ctl.SetBitrateRequest, value);
				if (ret < 0)
					throw new Exception("Encoder error - " + ((Errors)ret).ToString());
			}
		}

		/// <summary>
		/// Gets or sets whether Forward Error Correction is enabled.
		/// </summary>
		public bool ForwardErrorCorrection
		{
			get
			{
				if (encoder == IntPtr.Zero)
					throw new ObjectDisposedException("OpusEncoder");

				int ret = NativeMethods.opus_encoder_ctl(encoder, Ctl.GetInbandFecRequest, out int fec);
				if (ret < 0)
					throw new Exception("Encoder error - " + ((Errors)ret).ToString());

				return fec > 0;
			}

			set
			{
				if (encoder == IntPtr.Zero)
					throw new ObjectDisposedException("OpusEncoder");

				var ret = NativeMethods.opus_encoder_ctl(encoder, Ctl.SetInbandFecRequest, value ? 1 : 0);
				if (ret < 0)
					throw new Exception("Encoder error - " + ((Errors)ret).ToString());
			}
		}

		~OpusEncoder()
		{
			Dispose();
		}

		private bool disposed;
		public void Dispose()
		{
			if (disposed)
				return;

			GC.SuppressFinalize(this);

			if (encoder != IntPtr.Zero)
			{
				NativeMethods.opus_encoder_destroy(encoder);
				encoder = IntPtr.Zero;
			}

			disposed = true;
		}
	}
}
