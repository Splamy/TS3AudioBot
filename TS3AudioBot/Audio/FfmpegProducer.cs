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
	using Config;
	using Helper;
	using System;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Globalization;
	using System.Text.RegularExpressions;
	using System.Threading;
	using TS3Client.Audio;

	public class FfmpegProducer : IAudioPassiveProducer, ISampleInfo, IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Regex FindDurationMatch = new Regex(@"^\s*Duration: (\d+):(\d\d):(\d\d).(\d\d)", Util.DefaultRegexConfig);
		private const string PreLinkConf = "-hide_banner -nostats -i \"";
		private const string PostLinkConf = "\" -ac 2 -ar 48000 -f s16le -acodec pcm_s16le pipe:1";
		private readonly TimeSpan retryOnDropBeforeEnd = TimeSpan.FromSeconds(10);

		private readonly ConfToolsFfmpeg config;

		public event EventHandler OnSongEnd;

		private string lastLink;
		private ActiveFfmpegInstance ffmpegInstance;

		public int SampleRate { get; } = 48000;
		public int Channels { get; } = 2;
		public int BitsPerSample { get; } = 16;

		public FfmpegProducer(ConfToolsFfmpeg config)
		{
			this.config = config;
		}

		public E<string> AudioStart(string url) => StartFfmpegProcess(url, TimeSpan.Zero);

		public E<string> AudioStop()
		{
			StopFfmpegProcess();
			return R.Ok;
		}

		public TimeSpan Length => GetCurrentSongLength();

		public TimeSpan Position
		{
			get => ffmpegInstance?.AudioTimer.SongPosition ?? TimeSpan.Zero;
			set => SetPosition(value);
		}

		public int Read(byte[] buffer, int offset, int length, out Meta meta)
		{
			meta = null;
			bool triggerEndSafe = false;
			int read;

			var instance = ffmpegInstance;

			if (instance is null)
				return 0;

			read = instance.FfmpegProcess.StandardOutput.BaseStream.Read(buffer, 0, length);

			if (read == 0)
			{
				// check for premature connection drop
				if (instance.FfmpegProcess.HasExited && !instance.hasTriedToReconnectAudio)
				{
					var expectedStopLength = GetCurrentSongLength();
					Log.Trace("Expected song length {0}", expectedStopLength);
					if (expectedStopLength != TimeSpan.Zero)
					{
						var actualStopPosition = instance.AudioTimer.SongPosition;
						Log.Trace("Actual song position {0}", actualStopPosition);
						if (actualStopPosition + retryOnDropBeforeEnd < expectedStopLength)
						{
							Log.Debug("Connection to song lost, retrying at {0}", actualStopPosition);
							instance.hasTriedToReconnectAudio = true;
							var newInstance = SetPosition(actualStopPosition);
							if (newInstance.Ok)
							{
								newInstance.Value.hasTriedToReconnectAudio = true;
								return 0;
							}
							else
							{
								Log.Debug("Retry failed {0}", newInstance.Error);
								triggerEndSafe = true;
							}
						}
					}
				}

				if (instance.FfmpegProcess.HasExited)
				{
					Log.Trace("Ffmpeg has exited with {0}", instance.FfmpegProcess.ExitCode);
					AudioStop();
					triggerEndSafe = true;
				}
			}

			if (triggerEndSafe)
			{
				OnSongEnd?.Invoke(this, EventArgs.Empty);
				return 0;
			}

			instance.hasTriedToReconnectAudio = false;
			instance.AudioTimer.PushBytes(read);
			return read;
		}

		private R<ActiveFfmpegInstance, string> SetPosition(TimeSpan value)
		{
			if (value < TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(value));
			return StartFfmpegProcess(lastLink, value,
				$"-ss {value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)}",
				$"-ss {value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)}");
		}

		private R<ActiveFfmpegInstance, string> StartFfmpegProcess(string url, TimeSpan offset, string extraPreParam = null, string extraPostParam = null)
		{
			Log.Trace("Start request {0}", url);
			try
			{
				StopFfmpegProcess();

				var newInstance = new ActiveFfmpegInstance()
				{
					FfmpegProcess = new Process
					{
						StartInfo = new ProcessStartInfo
						{
							FileName = config.Path.Value,
							Arguments = string.Concat(extraPreParam, " ", PreLinkConf, url, PostLinkConf, " ", extraPostParam),
							RedirectStandardOutput = true,
							RedirectStandardInput = true,
							RedirectStandardError = true,
							UseShellExecute = false,
							CreateNoWindow = true,
						},
						EnableRaisingEvents = true,
					},
					AudioTimer = new PreciseAudioTimer(this)
					{
						SongPositionOffset = offset,
					}
				};

				Log.Trace("Starting with {0}", newInstance.FfmpegProcess.StartInfo.Arguments);
				newInstance.FfmpegProcess.ErrorDataReceived += newInstance.FfmpegProcess_ErrorDataReceived;
				newInstance.FfmpegProcess.Start();
				newInstance.FfmpegProcess.BeginErrorReadLine();

				lastLink = url;

				newInstance.AudioTimer.Start();

				var oldInstance = Interlocked.Exchange(ref ffmpegInstance, newInstance);
				oldInstance?.Close();

				return newInstance;
			}
			catch (Win32Exception ex)
			{
				var error = $"Ffmpeg could not be found ({ex.Message})";
				Log.Warn(ex, error);
				return error;
			}
			catch (Exception ex)
			{
				var error = $"Unable to create stream ({ex.Message})";
				Log.Warn(ex, error);
				return error;
			}
		}

		private void StopFfmpegProcess()
		{
			var oldInstance = Interlocked.Exchange(ref ffmpegInstance, null);
			oldInstance?.Close();
		}

		private TimeSpan GetCurrentSongLength()
		{
			var instance = ffmpegInstance;
			if (instance is null)
				return TimeSpan.Zero;

			return instance.ParsedSongLength ?? TimeSpan.Zero;
		}

		public void Dispose()
		{
			StopFfmpegProcess();
		}

		private class ActiveFfmpegInstance
		{
			public Process FfmpegProcess { get; set; }
			public bool HasIcyTag { get; private set; } = false;
			public bool hasTriedToReconnectAudio;
			public PreciseAudioTimer AudioTimer { get; set; }
			public TimeSpan? ParsedSongLength { get; set; } = null;

			public void Close()
			{
				try
				{
					if (!FfmpegProcess.HasExited)
						FfmpegProcess.Kill();
					else
						FfmpegProcess.Close();
				}
				catch (InvalidOperationException) { }
			}

			public void FfmpegProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
			{
				if (e.Data is null)
					return;

				if (sender != FfmpegProcess)
					throw new InvalidOperationException("Wrong process associated to event");

				if (!ParsedSongLength.HasValue)
				{
					var match = FindDurationMatch.Match(e.Data);
					if (!match.Success)
						return;

					int hours = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
					int minutes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
					int seconds = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
					int millisec = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) * 10;
					ParsedSongLength = new TimeSpan(0, hours, minutes, seconds, millisec);
				}

				if (!HasIcyTag && e.Data.AsSpan().TrimStart().StartsWith("icy-".AsSpan()))
				{
					HasIcyTag = true;
				}
			}
		}
	}
}
