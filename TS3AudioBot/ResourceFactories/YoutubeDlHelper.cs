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

namespace TS3AudioBot.ResourceFactories
{
	using Helper;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Diagnostics;
	using System.ComponentModel;

	static class YoutubeDlHelper
	{
		public static YoutubeFactoryData DataObj { private get; set; }
		private static string YoutubeDlPath => DataObj?.YoutubedlPath;

		public static R<Tuple<string, IList<string>>> FindAndRunYoutubeDl(string id)
		{
			var ytdlPath = FindYoutubeDl(id);
			if (ytdlPath == null)
				return "Youtube-Dl could not be found. The song/video cannot be played due to restrictions";

			return RunYoutubeDl(ytdlPath.Item1, ytdlPath.Item2);
		}

		public static Tuple<string, string> FindYoutubeDl(string id)
		{
			string param = $"--no-warnings --get-title --get-url --id {id}";

			// Default path youtube-dl is suggesting to install
			const string defaultYtDlPath = "/usr/local/bin/youtube-dl";
			if (File.Exists(defaultYtDlPath))
				return new Tuple<string, string>(defaultYtDlPath, param);

			if (YoutubeDlPath == null)
				return null;

			// Example: /home/teamspeak/youtube-dl where 'youtube-dl' is the binary
			string fullCustomPath = Path.GetFullPath(YoutubeDlPath);
			if (File.Exists(fullCustomPath) || File.Exists(fullCustomPath + ".exe"))
				return new Tuple<string, string>(fullCustomPath, param);

			// Example: /home/teamspeak where the binary 'youtube-dl' lies in ./teamspeak/
			string fullCustomPathWithoutFile = Path.Combine(fullCustomPath, "youtube-dl");
			if (File.Exists(fullCustomPathWithoutFile) || File.Exists(fullCustomPathWithoutFile + ".exe"))
				return new Tuple<string, string>(fullCustomPathWithoutFile, param);

			// Example: /home/teamspeak/youtube-dl where 'youtube-dl' is the github project folder
			string fullCustomPathGhProject = Path.Combine(fullCustomPath, "youtube_dl", "__main__.py");
			if (File.Exists(fullCustomPathGhProject))
				return new Tuple<string, string>("python", $"\"{fullCustomPathGhProject}\" {param}");

			return null;
		}

		public static R<Tuple<string, IList<string>>> RunYoutubeDl(string path, string args)
		{
			try
			{
				using (var tmproc = new Process())
				{
					tmproc.StartInfo.FileName = path;
					tmproc.StartInfo.Arguments = args;
					tmproc.StartInfo.UseShellExecute = false;
					tmproc.StartInfo.CreateNoWindow = true;
					tmproc.StartInfo.RedirectStandardOutput = true;
					tmproc.StartInfo.RedirectStandardError = true;
					tmproc.Start();
					tmproc.WaitForExit(10000);

					using (var reader = tmproc.StandardError)
					{
						string result = reader.ReadToEnd();
						if (!string.IsNullOrEmpty(result))
						{
							Log.Write(Log.Level.Error, "youtube-dl failed to load the resource:\n{0}", result);
							return "youtube-dl failed to load the resource";
						}
					}

					return ParseResponse(tmproc.StandardOutput);
				}
			}
			catch (Win32Exception) { return "Failed to run youtube-dl"; }
		}

		public static Tuple<string, IList<string>> ParseResponse(StreamReader stream)
		{
			string title = stream.ReadLine();

			var urlOptions = new List<string>();
			string line;
			while ((line = stream.ReadLine()) != null)
				urlOptions.Add(line);

			return new Tuple<string, IList<string>>(title, urlOptions);
		}
	}
}
