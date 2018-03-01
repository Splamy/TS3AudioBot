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
	using System;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Globalization;
	using System.Text.RegularExpressions;
	using TS3Client.Audio;

	public class FfmpegProducer : IAudioPassiveProducer, ISampleInfo, IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Regex FindDurationMatch = new Regex(@"^\s*Duration: (\d+):(\d\d):(\d\d).(\d\d)", Util.DefaultRegexConfig);
		private const string PreLinkConf = "-hide_banner -nostats -i \"";
		private const string PostLinkConf = "\" -ac 2 -ar 48000 -f s16le -acodec pcm_s16le pipe:1";
		private readonly TimeSpan retryOnDropBeforeEnd = TimeSpan.FromSeconds(10);
		private readonly object ffmpegLock = new object();

		private readonly Ts3FullClientData ts3FullClientData;

		public event EventHandler OnSongEnd;

		private readonly PreciseAudioTimer audioTimer;
		private string lastLink;
		private Process ffmpegProcess;
		private TimeSpan? parsedSongLength;
		private bool hasTriedToReconnectAudio;

		public int SampleRate { get; } = 48000;
		public int Channels { get; } = 2;
		public int BitsPerSample { get; } = 16;

		public FfmpegProducer(Ts3FullClientData tfcd)
		{
			ts3FullClientData = tfcd;
			audioTimer = new PreciseAudioTimer(this);
		}

		public R AudioStart(string url) => StartFfmpegProcess(url);

		public R AudioStop()
		{
			audioTimer.Stop();
			StopFfmpegProcess();
			return R.OkR;
		}

		public TimeSpan Length => GetCurrentSongLength();

		public TimeSpan Position
		{
			get => audioTimer.SongPosition;
			set
			{
				if (value < TimeSpan.Zero || value > Length)
					throw new ArgumentOutOfRangeException(nameof(value));
				AudioStop();
				StartFfmpegProcess(lastLink,
					$"-ss {value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)}",
					$"-ss {value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)}");
				audioTimer.SongPositionOffset = value;
			}
		}

		public int Read(byte[] buffer, int offset, int length, out Meta meta)
		{
			meta = null;
			bool triggerEndSafe = false;
			int read;

			lock (ffmpegLock)
			{
				if (ffmpegProcess == null)
					return 0;

				read = ffmpegProcess.StandardOutput.BaseStream.Read(buffer, 0, length);
				if (read == 0)
				{
					// check for premature connection drop
					if (ffmpegProcess.HasExited && !hasTriedToReconnectAudio)
					{
						var expectedStopLength = GetCurrentSongLength();
						Log.Trace("Expected song length {0}", expectedStopLength);
						if (expectedStopLength != TimeSpan.Zero)
						{
							var actualStopPosition = audioTimer.SongPosition;
							Log.Trace("Actual song position {0}", actualStopPosition);
							if (actualStopPosition + retryOnDropBeforeEnd < expectedStopLength)
							{
								Log.Debug("Connection to song lost, retrying at {0}", actualStopPosition);
								hasTriedToReconnectAudio = true;
								Position = actualStopPosition;
								return 0;
							}
						}
					}

					if (ffmpegProcess.HasExited)
					{
						Log.Trace("Ffmpeg has exited with {0}", ffmpegProcess.ExitCode);
						AudioStop();
						triggerEndSafe = true;
					}
				}
			}

			if (triggerEndSafe)
			{
				OnSongEnd?.Invoke(this, EventArgs.Empty);
				return 0;
			}

			hasTriedToReconnectAudio = false;
			audioTimer.PushBytes(read);
			return read;
		}

		public R StartFfmpegProcess(string url, string extraPreParam = null, string extraPostParam = null)
		{
			Log.Trace("Start request {0}", url);
			try
			{
				lock (ffmpegLock)
				{
					StopFfmpegProcess();

					ffmpegProcess = new Process
					{
						StartInfo = new ProcessStartInfo
						{
							FileName = ts3FullClientData.FfmpegPath,
							Arguments = string.Concat(extraPreParam, " ", PreLinkConf, url, PostLinkConf, " ", extraPostParam),
							RedirectStandardOutput = true,
							RedirectStandardInput = true,
							RedirectStandardError = true,
							UseShellExecute = false,
							CreateNoWindow = true,
						}
					};
					Log.Trace("Starting with {0}", ffmpegProcess.StartInfo.Arguments);
					ffmpegProcess.ErrorDataReceived += FfmpegProcess_ErrorDataReceived;
					ffmpegProcess.Start();
					ffmpegProcess.BeginErrorReadLine();

					lastLink = url;
					parsedSongLength = null;

					audioTimer.SongPositionOffset = TimeSpan.Zero;
					audioTimer.Start();
					return R.OkR;
				}
			}
			catch (Win32Exception ex) { return $"Ffmpeg could not be found ({ex.Message})"; }
			catch (Exception ex) { return $"Unable to create stream ({ex.Message})"; }
		}

		private void FfmpegProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
		{
			if (e.Data == null)
				return;

			lock (ffmpegLock)
			{
				if (parsedSongLength.HasValue)
					return;

				var match = FindDurationMatch.Match(e.Data);
				if (!match.Success)
					return;

				if (sender != ffmpegProcess)
					return;

				int hours = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
				int minutes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
				int seconds = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
				int millisec = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) * 10;
				parsedSongLength = new TimeSpan(0, hours, minutes, seconds, millisec);
			}
		}

		private void StopFfmpegProcess()
		{
			// TODO somehow bypass lock
			lock (ffmpegLock)
			{
				if (ffmpegProcess == null)
					return;

				try
				{
					if (!ffmpegProcess.HasExited)
						ffmpegProcess.Kill();
					else
						ffmpegProcess.Close();
				}
				catch (InvalidOperationException) { }
				ffmpegProcess = null;
			}
		}

		private TimeSpan GetCurrentSongLength()
		{
			lock (ffmpegLock)
			{
				if (ffmpegProcess == null)
					return TimeSpan.Zero;

				if (parsedSongLength.HasValue)
					return parsedSongLength.Value;

				return TimeSpan.Zero;
			}
		}

		public void Dispose()
		{
			// TODO close ffmpeg if open
		}
	}
}
