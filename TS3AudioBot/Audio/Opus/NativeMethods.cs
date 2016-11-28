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

namespace TS3AudioBot.Audio.Opus
{
	using System;
	using System.Runtime.InteropServices;

	/// <summary>
	/// Wraps the Opus API.
	/// </summary>
	internal class NativeMethods
	{
		[DllImport("libopus", CallingConvention = CallingConvention.Cdecl)]
		internal static extern IntPtr opus_encoder_create(int Fs, int channels, int application, out IntPtr error);

		[DllImport("libopus", CallingConvention = CallingConvention.Cdecl)]
		internal static extern void opus_encoder_destroy(IntPtr encoder);

		[DllImport("libopus", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int opus_encode(IntPtr st, byte[] pcm, int frame_size, IntPtr data, int max_data_bytes);

		[DllImport("libopus", CallingConvention = CallingConvention.Cdecl)]
		internal static extern IntPtr opus_decoder_create(int Fs, int channels, out IntPtr error);

		[DllImport("libopus", CallingConvention = CallingConvention.Cdecl)]
		internal static extern void opus_decoder_destroy(IntPtr decoder);

		[DllImport("libopus", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int opus_decode(IntPtr st, byte[] data, int len, IntPtr pcm, int frame_size, int decode_fec);

		[DllImport("libopus", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int opus_encoder_ctl(IntPtr st, Ctl request, int value);

		[DllImport("libopus", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int opus_encoder_ctl(IntPtr st, Ctl request, out int value);
	}

	public enum Ctl : int
	{
		SetBitrateRequest = 4002,
		GetBitrateRequest = 4003,
		SetInbandFECRequest = 4012,
		GetInbandFECRequest = 4013
	}

	/// <summary>
	/// Supported coding modes.
	/// </summary>
	public enum Application
	{
		/// <summary>
		/// Best for most VoIP/videoconference applications where listening quality and intelligibility matter most.
		/// </summary>
		Voip = 2048,
		/// <summary>
		/// Best for broadcast/high-fidelity application where the decoded audio should be as close as possible to input.
		/// </summary>
		Audio = 2049,
		/// <summary>
		/// Only use when lowest-achievable latency is what matters most. Voice-optimized modes cannot be used.
		/// </summary>
		Restricted_LowLatency = 2051
	}

	public enum Errors
	{
		/// <summary>
		/// No error.
		/// </summary>
		OK = 0,
		/// <summary>
		/// One or more invalid/out of range arguments.
		/// </summary>
		BadArg = -1,
		/// <summary>
		/// The mode struct passed is invalid.
		/// </summary>
		BufferToSmall = -2,
		/// <summary>
		/// An internal error was detected.
		/// </summary>
		InternalError = -3,
		/// <summary>
		/// The compressed data passed is corrupted.
		/// </summary>
		InvalidPacket = -4,
		/// <summary>
		/// Invalid/unsupported request number.
		/// </summary>
		Unimplemented = -5,
		/// <summary>
		/// An encoder or decoder structure is invalid or already freed.
		/// </summary>
		InvalidState = -6,
		/// <summary>
		/// Memory allocation has failed.
		/// </summary>
		AllocFail = -7
	}
}
