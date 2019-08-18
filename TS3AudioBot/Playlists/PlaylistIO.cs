// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Playlists
{
	using LiteDB;
	using Newtonsoft.Json;
	using Org.BouncyCastle.Utilities.Collections;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using TS3AudioBot.Config;
	using TS3AudioBot.Helper;
	using TS3AudioBot.Localization;
	using TS3AudioBot.ResourceFactories;

	public class PlaylistIO : IDisposable
	{
		private readonly ConfBot confBot;
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private readonly Dictionary<string, Playlist> playlistCache;
		private readonly HashSet<string> dirtyList;
		private readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();
		private const string PlaylistsFolder = "playlists";
		private string PlaylistsPath => Path.Combine(confBot.LocalConfigDir, PlaylistsFolder);

		public PlaylistIO(ConfBot confBot)
		{
			this.confBot = confBot;
			Util.Init(out playlistCache);
			Util.Init(out dirtyList);
		}

		private FileInfo NameToFile(string name)
		{
			return new FileInfo(Path.Combine(confBot.LocalConfigDir, PlaylistsFolder, name));
		}

		public R<Playlist, LocalStr> Read(string name) => ReadInternal(name, false, false);

		private R<Playlist, LocalStr> ReadInternal(string name, bool hasReadLock, bool hasWriteLock)
		{
			try
			{
				if (!hasReadLock && !hasWriteLock)
				{
					rwLock.EnterReadLock();
					hasReadLock = true;
				}

				if (playlistCache.TryGetValue(name, out var playlist))
				{
					return playlist;
				}

				if (!hasWriteLock)
				{
					if (hasReadLock)
					{
						rwLock.ExitReadLock();
						hasReadLock = false;
					}

					rwLock.EnterWriteLock();
					hasWriteLock = true;
				}

				var result = ReadFromFile(name);

				if (result.Ok)
				{
					playlistCache.Add(name, result.Value);
					return result.Value;
				}
				else
				{
					return result.Error;
				}
			}
			finally
			{
				if (hasReadLock)
					rwLock.ExitReadLock();
				if (hasWriteLock)
					rwLock.ExitWriteLock();
			}
		}

		private R<Playlist, LocalStr> ReadFromFile(string name, bool headOnly = false)
		{
			var fi = NameToFile(name);
			if (!fi.Exists)
				return new LocalStr(strings.error_playlist_not_found);

			using (var sr = new StreamReader(fi.Open(System.IO.FileMode.Open, FileAccess.Read, FileShare.Read), Util.Utf8Encoder))
			{
				var plist = new Playlist(name);

				// Info: version:<num>
				// Info: owner:<uid>

				string line;
				int version = 1;

				// read header
				while ((line = sr.ReadLine()) != null)
				{
					if (string.IsNullOrEmpty(line))
						break;

					var kvp = line.Split(new[] { ':' }, 2);
					if (kvp.Length < 2) continue;

					string key = kvp[0];
					string value = kvp[1];

					switch (key)
					{
					case "version":
						version = int.Parse(value);
						if (version > 2)
							return new LocalStr("The file version is too new and can't be read."); // LOC: TODO
						break;
					}
				}

				if (headOnly)
					return plist;

				// read content
				while ((line = sr.ReadLine()) != null)
				{
					var kvp = line.Split(new[] { ':' }, 2);
					if (kvp.Length < 2) continue;

					string key = kvp[0];
					string value = kvp[1];

					switch (key)
					{
					case "rs":
						{
							var rskvp = value.Split(new[] { ':' }, 2);
							if (kvp.Length < 2)
							{
								Log.Warn("Erroneus playlist split count: {0}", line);
								continue;
							}
							string optOwner = rskvp[0];
							string content = rskvp[1];

							var rsSplit = content.Split(new[] { ',' }, 3);
							if (rsSplit.Length < 3)
								goto default;
							if (!string.IsNullOrWhiteSpace(rsSplit[0]))
								plist.Items.Add(new PlaylistItem(new AudioResource(Uri.UnescapeDataString(rsSplit[1]), Uri.UnescapeDataString(rsSplit[2]), rsSplit[0])));
							else
								goto default;
							break;
						}

					case "rsj":
						var rsjdata = JsonConvert.DeserializeAnonymousType(value, new
						{
							type = string.Empty,
							resid = string.Empty,
							title = string.Empty,
						});
						plist.Items.Add(new PlaylistItem(new AudioResource(rsjdata.resid, rsjdata.title, rsjdata.type)));
						break;

					case "id":
					case "ln":
						Log.Warn("Deprecated playlist data block: {0}", line);
						break;

					default:
						Log.Warn("Erroneus playlist data block: {0}", line);
						break;
					}
				}
				return plist;
			}
		}

		public E<LocalStr> Write(string name, IReadOnlyPlaylist list)
		{
			try
			{
				rwLock.EnterWriteLock();

				var result = WriteToFile(name, list);
				dirtyList.Remove(name);
				return result;
			}
			finally
			{
				rwLock.ExitWriteLock();
			}
		}

		private E<LocalStr> WriteToFile(string name, IReadOnlyPlaylist plist)
		{
			var fi = NameToFile(name);
			var dir = fi.Directory;
			if (!dir.Exists)
				dir.Create();

			using (var sw = new StreamWriter(fi.Open(System.IO.FileMode.Create, FileAccess.Write, FileShare.Read), Util.Utf8Encoder))
			{
				sw.WriteLine("version:2");
				sw.WriteLine();

				using (var json = new JsonTextWriter(sw))
				{
					json.Formatting = Formatting.None;

					foreach (var pli in plist.Items)
					{
						sw.Write("rsj:");
						json.WriteStartObject();
						json.WritePropertyName("type");
						json.WriteValue(pli.Resource.AudioType);
						json.WritePropertyName("resid");
						json.WriteValue(pli.Resource.ResourceId);
						if (pli.Resource.ResourceTitle != null)
						{
							json.WritePropertyName("title");
							json.WriteValue(pli.Resource.ResourceTitle);
						}
						json.WriteEndObject();
						json.Flush();
						sw.WriteLine();
					}
				}
			}
			return R.Ok;
		}

		public E<LocalStr> Delete(string name)
		{
			try
			{
				rwLock.EnterWriteLock();
				return DeleteInternal(name);
			}
			finally
			{
				rwLock.ExitWriteLock();
			}
		}

		private E<LocalStr> DeleteInternal(string name)
		{
			var fi = NameToFile(name);
			bool cached = playlistCache.ContainsKey(name);

			if (!cached && !fi.Exists)
				return new LocalStr(strings.error_playlist_not_found);

			playlistCache.Remove(name);
			dirtyList.Remove(name);

			try
			{
				fi.Delete();
				return R.Ok;
			}
			catch (IOException) { return new LocalStr(strings.error_io_in_use); }
			catch (System.Security.SecurityException) { return new LocalStr(strings.error_io_missing_permission); }
		}

		public E<LocalStr> Move(string name, string newName)
		{
			try
			{
				rwLock.EnterWriteLock();
				return MoveInternal(name, newName);
			}
			finally
			{
				rwLock.ExitWriteLock();
			}
		}

		private E<LocalStr> MoveInternal(string name, string newName)
		{
			var cached = playlistCache.ContainsKey(name);
			var dirty = cached && dirtyList.Contains(name);

			if (cached && dirty)
			{
				var playlist = playlistCache[name];
				var result = WriteToFile(name, playlist);
				if (!result)
					return result.Error;
				playlistCache[newName] = playlist;

				DeleteInternal(name);
			}
			else
			{
				if (cached)
				{
					playlistCache[newName] = playlistCache[name];
					playlistCache.Remove(name);
				}

				var fi = NameToFile(name);
				if (!fi.Exists)
					return new LocalStr(strings.error_playlist_not_found);
				var fiNew = NameToFile(newName);
				fi.MoveTo(fiNew.FullName);
			}
			return R.Ok;
		}

		public R<PlaylistInfo[], LocalStr> ListPlaylists(string pattern)
		{
			var di = new DirectoryInfo(PlaylistsPath);
			if (!di.Exists)
				return Array.Empty<PlaylistInfo>();

			IEnumerable<FileInfo> fileEnu;
			if (string.IsNullOrEmpty(pattern))
				fileEnu = di.EnumerateFiles();
			else
				fileEnu = di.EnumerateFiles(pattern, SearchOption.TopDirectoryOnly); // TODO exceptions

			return fileEnu.Select(fi => new PlaylistInfo { FileName = fi.Name }).ToArray();
		}

		public void Flush()
		{
			try
			{
				rwLock.EnterWriteLock();

				foreach (var name in dirtyList)
				{
					var plist = playlistCache[name];
					WriteToFile(name, plist);
				}

				dirtyList.Clear();
			}
			finally
			{
				rwLock.ExitWriteLock();
			}
		}

		public void Dispose()
		{
			Flush();

			rwLock.Dispose();
		}
	}

	public class DbPlaylistV1
	{
		public ObjectId Id { get; set; }
		public string Name { get; set; }
		public string Bot { get; set; }

		//public PlaylistLocation Share { get; set; }

		public List<DbPlaylistEntryV1> Data { get; set; } = new List<DbPlaylistEntryV1>();
	}

	public class DbPlaylistEntryV1
	{
		public string Title { get; set; }
		public string Url { get; set; }
	}
}
