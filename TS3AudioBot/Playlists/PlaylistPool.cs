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
	using Newtonsoft.Json;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using TS3AudioBot.Helper;
	using TS3AudioBot.Localization;
	using TS3AudioBot.ResourceFactories;

	public class PlaylistPool : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private readonly Dictionary<string, Playlist> playlistCache;
		private readonly Dictionary<string, FileInfo> dirtyList;
		private readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();
		private readonly TimeSpan FlushDelay = TimeSpan.FromSeconds(30);

		public PlaylistPool()
		{
			Util.Init(out playlistCache);
			Util.Init(out dirtyList);
		}

		public R<Playlist, LocalStr> Read(FileInfo fi) => ReadInternal(fi, false, false);

		private R<Playlist, LocalStr> ReadInternal(FileInfo fi, bool hasReadLock, bool hasWriteLock)
		{
			try
			{
				if (!hasReadLock && !hasWriteLock)
				{
					rwLock.EnterReadLock();
					hasReadLock = true;
				}

				if (playlistCache.TryGetValue(fi.FullName, out var playlist))
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

				var result = ReadFromFile(fi);

				if (result.Ok)
				{
					playlistCache.Add(fi.FullName, result.Value);
				}

				return result;
			}
			finally
			{
				if (hasReadLock)
					rwLock.ExitReadLock();
				if (hasWriteLock)
					rwLock.ExitWriteLock();
			}
		}

		private R<Playlist, LocalStr> ReadFromFile(FileInfo fi, bool headOnly = false)
		{
			if (!fi.Exists)
				return new LocalStr(strings.error_playlist_not_found);

			using (var sr = new StreamReader(fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read), Util.Utf8Encoder))
			{
				var plist = new Playlist(fi.Name);

				// Info: version:<num>
				// Info: owner:<uid>
				// Line: <kind>:<data,data,..>:<opt-title>

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

					case "owner":
						if (plist.OwnerUid != null)
						{
							Log.Warn("Invalid playlist file: duplicate userid");
							return new LocalStr(strings.error_playlist_broken_file);
						}

						if (version == 2)
						{
							plist.OwnerUid = value;
						}
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
							title = string.Empty
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

		public E<LocalStr> Write(Playlist list, FileInfo fi)
		{
			try
			{
				rwLock.EnterWriteLock();

				// todo flush cache

				var result = WriteToFile(list, fi);
				dirtyList.Remove(fi.FullName);
				return result;
			}
			finally
			{
				rwLock.ExitWriteLock();
			}
		}

		private E<LocalStr> WriteToFile(Playlist plist, FileInfo fi)
		{
			if (fi.Exists)
			{
				var tempList = LoadChecked(ReadInternal(fi, false, true), plist.OwnerUid);
				if (!tempList)
					return tempList.OnlyError();
			}

			using (var sw = new StreamWriter(fi.Open(FileMode.Create, FileAccess.Write, FileShare.Read), Util.Utf8Encoder))
			{
				sw.WriteLine("version:2");
				if (plist.OwnerUid != null)
				{
					sw.Write("owner:");
					sw.Write(plist.OwnerUid);
					sw.WriteLine();
				}

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

		private static R<Playlist, LocalStr> LoadChecked(in R<Playlist, LocalStr> loadResult, string ownerUid)
		{
			if (!loadResult)
				return new LocalStr($"{strings.error_playlist_broken_file} ({loadResult.Error.Str})");
			if (loadResult.Value.OwnerUid != null && loadResult.Value.OwnerUid != ownerUid)
				return new LocalStr(strings.error_playlist_cannot_access_not_owned);
			return loadResult;
		}

		public E<LocalStr> Delete(FileInfo fi, string requestingClientUid, bool force)
		{
			try
			{
				rwLock.EnterWriteLock();
				return DeleteInternal(fi, requestingClientUid, force);
			}
			finally
			{
				rwLock.ExitWriteLock();
			}
		}

		private E<LocalStr> DeleteInternal(FileInfo fi, string requestingClientUid, bool force)
		{
			bool cached = playlistCache.TryGetValue(fi.FullName, out var playlist);

			if (!cached && !fi.Exists)
				return new LocalStr(strings.error_playlist_not_found);

			if (!force)
			{
				if (!cached)
				{
					var result = ReadFromFile(fi, true);
					if (!result)
						return result.OnlyError();
					playlist = result.Value;
				}

				var tempList = LoadChecked(playlist, requestingClientUid);
				if (!tempList)
					return tempList.OnlyError();
			}

			playlistCache.Remove(fi.FullName);
			dirtyList.Remove(fi.FullName);

			try
			{
				fi.Delete();
				return R.Ok;
			}
			catch (IOException) { return new LocalStr(strings.error_io_in_use); }
			catch (System.Security.SecurityException) { return new LocalStr(strings.error_io_missing_permission); }
		}

		public void Dirty(FileInfo fi)
		{
			if (FlushDelay == TimeSpan.Zero)
			{
				Flush();
				return;
			}

			try
			{
				rwLock.EnterWriteLock();

				Task.Delay(FlushDelay).ContinueWith(t => Flush());
			}
			finally
			{
				rwLock.ExitWriteLock();
			}
		}

		public void Flush()
		{
			try
			{
				rwLock.EnterWriteLock();

				foreach (var kvp in dirtyList)
				{
					var plist = playlistCache[kvp.Key];
					WriteToFile(plist, kvp.Value);
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
			rwLock.Dispose();
		}
	}
}
