// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Config;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;

namespace TS3AudioBot.ResourceFactories
{
	public static class YoutubeDlHelper
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		public static ConfPath? DataObj { private get; set; }
		private static string? YoutubeDlPath => DataObj?.Path.Value;

		private const string ParamGetSingleVideo = " --no-warnings --dump-json --id --";
		private const string ParamGetPlaylist = "--no-warnings --yes-playlist --flat-playlist --dump-single-json --id --";
		private const string ParamGetSearch = "--no-warnings --flat-playlist --dump-single-json -- ytsearch10:";

		public static async Task<JsonYtdlDump> GetSingleVideo(string id)
		{
			var ytdlPath = FindYoutubeDl();
			if (ytdlPath is null)
				throw Error.LocalStr(strings.error_ytdl_not_found);

			var param = $"{ytdlPath.Value.param}{ParamGetSingleVideo} {id}";
			return await RunYoutubeDl<JsonYtdlDump>(ytdlPath.Value.ytdlpath, param);
		}

		public static async Task<JsonYtdlPlaylistDump> GetPlaylistAsync(string url)
		{
			var ytdlPath = FindYoutubeDl();
			if (ytdlPath is null)
				throw Error.LocalStr(strings.error_ytdl_not_found);

			var param = $"{ytdlPath.Value.param}{ParamGetPlaylist} {url}";
			return await RunYoutubeDl<JsonYtdlPlaylistDump>(ytdlPath.Value.ytdlpath, param);
		}

		public static async Task<JsonYtdlPlaylistDump> GetSearchAsync(string text)
		{
			var ytdlPath = FindYoutubeDl();
			if (ytdlPath is null)
				throw Error.LocalStr(strings.error_ytdl_not_found);

			var param = $"{ytdlPath.Value.param}{ParamGetSearch}\"{text}\"";
			return await RunYoutubeDl<JsonYtdlPlaylistDump>(ytdlPath.Value.ytdlpath, param);
		}

		public static (string ytdlpath, string param)? FindYoutubeDl()
		{
			var youtubeDlPath = YoutubeDlPath;
			if (string.IsNullOrEmpty(youtubeDlPath))
			{
				// Default path youtube-dl is suggesting to install
				const string defaultYtDlPath = "/usr/local/bin/youtube-dl";
				if (File.Exists(defaultYtDlPath))
					return (defaultYtDlPath, "");

				// Default path most package managers install to
				const string defaultPkgManPath = "/usr/bin/youtube-dl";
				if (File.Exists(defaultPkgManPath))
					return (defaultPkgManPath, "");

				youtubeDlPath = Directory.GetCurrentDirectory();
			}

			string fullCustomPath;
			try { fullCustomPath = Path.GetFullPath(youtubeDlPath); }
			catch (ArgumentException ex)
			{
				Log.Warn(ex, "Your youtube-dl path may contain invalid characters");
				return null;
			}

			// Example: /home/teamspeak/youtube-dl where 'youtube-dl' is the binary
			if (File.Exists(fullCustomPath) || File.Exists(fullCustomPath + ".exe"))
				return (fullCustomPath, "");

			// Example: /home/teamspeak where the binary 'youtube-dl' lies in ./teamspeak/
			string fullCustomPathWithoutFile = Path.Combine(fullCustomPath, "youtube-dl");
			if (File.Exists(fullCustomPathWithoutFile) || File.Exists(fullCustomPathWithoutFile + ".exe"))
				return (fullCustomPathWithoutFile, "");

			// Example: /home/teamspeak/youtube-dl where 'youtube-dl' is the github project folder
			string fullCustomPathGhProject = Path.Combine(fullCustomPath, "youtube_dl", "__main__.py");
			if (File.Exists(fullCustomPathGhProject))
				return ("python", $"\"{fullCustomPathGhProject}\"");

			return null;
		}

		public static async Task<T> RunYoutubeDl<T>(string path, string args) where T : notnull
		{
			try
			{
				bool stdOutDone = false;
				var stdOut = new StringBuilder();
				var stdErr = new StringBuilder();

				using var tmproc = new Process();
				tmproc.StartInfo.FileName = path;
				tmproc.StartInfo.Arguments = args;
				tmproc.StartInfo.UseShellExecute = false;
				tmproc.StartInfo.CreateNoWindow = true;
				tmproc.StartInfo.RedirectStandardOutput = true;
				tmproc.StartInfo.RedirectStandardError = true;
				tmproc.EnableRaisingEvents = true;
				tmproc.Start();
				tmproc.OutputDataReceived += (s, e) =>
				{
					if (e.Data is null)
						stdOutDone = true;
					else
						stdOut.Append(e.Data);
				};
				tmproc.ErrorDataReceived += (s, e) => stdErr.Append(e.Data);
				tmproc.BeginOutputReadLine();
				tmproc.BeginErrorReadLine();
				await tmproc.WaitForExitAsync(TimeSpan.FromSeconds(20));

				if (!tmproc.HasExitedSafe())
				{
					try { tmproc.Kill(); }
					catch (Exception ex) { Log.Debug(ex, "Failed to kill"); }
				}

				var timeout = Stopwatch.StartNew();
				while (!stdOutDone)
				{
					if (timeout.Elapsed >= TimeSpan.FromSeconds(5))
					{
						stdErr.Append(strings.error_ytdl_empty_response).Append(" (timeout)");
						break;
					}
					await Task.Delay(50);
				}

				if (stdErr.Length > 0)
				{
					Log.Debug("youtube-dl failed to load the resource:\n{0}", stdErr);
					throw Error.LocalStr(strings.error_ytdl_song_failed_to_load);
				}

				return ParseResponse<T>(stdOut.ToString());
			}
			catch (Win32Exception ex)
			{
				Log.Error(ex, "Failed to run youtube-dl: {0}", ex.Message);
				throw Error.Exception(ex).LocalStr(strings.error_ytdl_failed_to_run);
			}
		}

		public static T ParseResponse<T>(string? json) where T : notnull
		{
			if (string.IsNullOrEmpty(json))
				throw Error.LocalStr(strings.error_ytdl_empty_response);

			try
			{

				return JsonConvert.DeserializeObject<T>(json);
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Failed to read youtube-dl json data");
				throw Error.Exception(ex).LocalStr(strings.error_media_internal_invalid);
			}
		}

		public static JsonYtdlFormat? FilterBest(IEnumerable<JsonYtdlFormat>? formats)
		{
			Log.Debug("Picking from options: {@formats}", formats);

			if (formats is null)
				return null;

			JsonYtdlFormat? best = null;
			foreach (var format in formats)
			{
				if (format.acodec == "none")
					continue;
				if (best == null
					|| format.abr > best.abr
					|| (format.vcodec == "none" && format.abr >= best.abr))
				{
					best = format;
				}
			}

			Log.Debug("Picked: {@format}", best);
			return best;
		}

		public static SongInfo MapToSongInfo(JsonYtdlDump dump)
		{
			return new SongInfo
			{
				Title = dump.title,
				Track = dump.track,
				Artist = dump.artist,
				Length = TimeSpan.FromSeconds(dump.duration)
			};
		}

		// https://stackoverflow.com/a/50461641/2444047
		/// <summary>
		/// Waits asynchronously for the process to exit.
		/// </summary>
		/// <param name="process">The process to wait for cancellation.</param>
		/// <param name="timeout">The maximum time to wait for exit before returning anyway.</param>
		/// <param name="cancellationToken">A cancellation token. If invoked, the task will return
		/// immediately as canceled.</param>
		/// <returns>A Task representing waiting for the process to end.</returns>
		public static async Task WaitForExitAsync(this Process process, TimeSpan timeout, CancellationToken cancellationToken = default)
		{
			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			void Process_Exited(object? sender, EventArgs e)
			{
				tcs.TrySetResult(true);
			}

			process.EnableRaisingEvents = true;
			process.Exited += Process_Exited;

			try
			{
				if (process.HasExited)
				{
					return;
				}

				var timoutTask = Task.Delay(timeout, cancellationToken);

				using (cancellationToken.Register(() => tcs.TrySetCanceled()))
				{
					await Task.WhenAny(tcs.Task, timoutTask);
				}
			}
			finally
			{
				process.Exited -= Process_Exited;
			}
		}
	}

#pragma warning disable CS0649, CS0169, IDE1006
	public abstract class JsonYtdlBase
	{
		public string? extractor { get; set; }
		public string? extractor_key { get; set; }
	}

	public class JsonYtdlDump : JsonYtdlBase
	{
		public string? title { get; set; }
		public string? track { get; set; }
		public string? artist { get; set; }
		// TODO int -> timespan converter
		public float duration { get; set; }
		public string? id { get; set; }
		public JsonYtdlFormat[]? formats { get; set; }
		public JsonYtdlFormat[]? requested_formats { get; set; }

		public string? AutoTitle => track ?? title;
	}

	public class JsonYtdlFormat
	{
		public string? vcodec { get; set; }
		public string? acodec { get; set; }
		/// <summary>audioBitRate</summary>
		public float? abr { get; set; }
		/// <summary>audioSampleRate</summary>
		public float? asr { get; set; }
		/// <summary>totalBitRate</summary>
		public float? tbr { get; set; }
		//public object http_headers { get; set; }
		public string? format { get; set; }
		public string? format_id { get; set; }
		public string? url { get; set; }
		public string? ext { get; set; }
	}

	public class JsonYtdlPlaylistDump : JsonYtdlBase
	{
		public string? id { get; set; }
		public string? title { get; set; }
		public JsonYtdlPlaylistEntry[]? entries { get; set; }
	}

	public class JsonYtdlPlaylistEntry
	{
		public string? title { get; set; }
		public string? id { get; set; }
	}
#pragma warning restore CS0649, CS0169, IDE1006
}
