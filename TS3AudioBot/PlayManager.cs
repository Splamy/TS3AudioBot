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

namespace TS3AudioBot
{
	using System;
	using Helper;
	using History;
	using ResourceFactories;
	using TS3Query.Messages;

	public class PlayManager
	{
		private MainBot botParent;
		private AudioFramework audioFramework => botParent.AudioFramework;
		private PlaylistManager playlistManager => botParent.PlaylistManager;
		private ResourceFactoryManager resourceFactoryManager => botParent.FactoryManager;
		private HistoryManager historyManager => botParent.HistoryManager;

		public PlayInfoEventArgs CurrentPlayData { get; private set; }
		public bool IsPlaying => CurrentPlayData != null;

		public event EventHandler BeforeResourceStarted;
		public event EventHandler<PlayInfoEventArgs> AfterResourceStarted;
		public event EventHandler<SongEndEventArgs> BeforeResourceStopped;
		public event EventHandler AfterResourceStopped;

		public PlayManager(MainBot parent)
		{
			botParent = parent;
		}

		public R Enqueue(ClientData invoker, AudioResource ar) => EnqueueInternal(invoker, new PlaylistItem(ar));
		public R Enqueue(ClientData invoker, string message, AudioType? type = null) => EnqueueInternal(invoker, new PlaylistItem(message, type));
		public R Enqueue(ClientData invoker, uint historyId) => EnqueueInternal(invoker, new PlaylistItem(historyId));

		private R EnqueueInternal(ClientData invoker, PlaylistItem pli)
		{
			pli.Meta.ResourceOwnerDbId = invoker.DatabaseId;
			playlistManager.AddToFreelist(pli);

			return R.OkR;
		}

		/// <summary>Playes the passed <see cref="AudioResource"/></summary>
		/// <param name="invoker">The invoker of this resource. Used for responses and association.</param>
		/// <param name="ar">The resource to load and play.</param>
		/// <param name="meta">Allows overriding certain settings for the resource. Can be null.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public R Play(ClientData invoker, AudioResource ar, MetaData meta = null)
		{
			var result = resourceFactoryManager.Load(ar);
			if (!result)
				return result.Message;
			return Play(invoker, result.Value, meta ?? new MetaData());
		}
		/// <summary>Playes the passed <see cref="PlayData.PlayResource"/></summary>
		/// <param name="invoker">The invoker of this resource. Used for responses and association.</param>
		/// <param name="audioType">The associated <see cref="AudioType"/> to a factory.</param>
		/// <param name="link">The link to resolve, load and play.</param>
		/// <param name="meta">Allows overriding certain settings for the resource. Can be null.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public R Play(ClientData invoker, string link, AudioType? type = null, MetaData meta = null)
		{
			var result = resourceFactoryManager.Load(link, type);
			if (!result)
				return result.Message;
			return Play(invoker, result.Value, meta ?? new MetaData());
		}
		public R Play(ClientData invoker, uint historyId, MetaData meta = null)
		{
			var getresult = historyManager.GetEntryById(historyId);
			if (!getresult)
				return getresult.Message;

			var loadresult = resourceFactoryManager.Load(getresult.Value.AudioResource);
			if (!loadresult)
				return loadresult.Message;

			return Play(invoker, loadresult.Value, meta ?? new MetaData());
		}
		public R Play(ClientData invoker, PlaylistItem item)
		{
			if (item == null)
				throw new ArgumentNullException(nameof(item));

			R lastResult = R.OkR;
			ClientData realInvoker = CurrentPlayData?.Invoker ?? invoker;

			if (item.HistoryId.HasValue)
			{
				lastResult = Play(realInvoker, item.HistoryId.Value, item.Meta);
				if (lastResult)
					return R.OkR;
			}
			if (!string.IsNullOrWhiteSpace(item.Link))
			{
				lastResult = Play(realInvoker, item.Link, item.AudioType, item.Meta);
				if (lastResult)
					return R.OkR;
			}
			if (item.Resource != null)
			{
				lastResult = Play(realInvoker, item.Resource, item.Meta);
				if (lastResult)
					return R.OkR;
			}
			return $"Playlist item could not be played ({lastResult.Message})";
		}

		public R Play(ClientData invoker, PlayResource play, MetaData meta)
		{
			if (!meta.FromPlaylist)
				meta.ResourceOwnerDbId = invoker.DatabaseId;

			// add optional beforestart here. maybe for blocking/interrupting etc.
			BeforeResourceStarted?.Invoke(this, new EventArgs());

			// pass the song to the AF to start it
			var result = audioFramework.StartResource(play, meta);
			if (!result) return result;

			// add it to our freelist for comfort
			if (!meta.FromPlaylist)
			{
				int index = playlistManager.InsertToFreelist(new PlaylistItem(play.BaseData, meta));
				playlistManager.Index = index;
			}

			// Log our resource in the history
			ulong owner = meta.ResourceOwnerDbId ?? invoker.DatabaseId;
			historyManager.LogAudioResource(new HistorySaveData(play.BaseData, owner));

			CurrentPlayData = new PlayInfoEventArgs(invoker, play, meta); // TODO meta as readonly
			AfterResourceStarted?.Invoke(this, CurrentPlayData);

			return R.OkR;
		}

		public R Next(ClientData invoker)
		{
			PlaylistItem pli = null;
			for (int i = 0; i < 10; i++)
			{
				if ((pli = playlistManager.Next()) == null) break;
				if (Play(invoker, pli))
					return R.OkR;
				// optional message here that playlist entry has been skipped
			}
			if (pli == null)
				return "No next song could be played";
			else
				return "A few songs failed to start, use !next to continue";
		}
		public R Previous(ClientData invoker)
		{
			PlaylistItem pli = null;
			for (int i = 0; i < 10; i++)
			{
				if ((pli = playlistManager.Previous()) == null) break;
				if (Play(invoker, pli))
					return R.OkR;
				// optional message here that playlist entry has been skipped
			}
			if (pli == null)
				return "No previous song could be played";
			else
				return "A few songs failed to start, use !previous to continue";
		}

		public void SongStoppedHook(object sender, SongEndEventArgs e)
		{
			BeforeResourceStopped?.Invoke(this, e);

			if (e.SongEndedByCallback && CurrentPlayData != null && Next(CurrentPlayData.Invoker))
				return;

			CurrentPlayData = null;
			AfterResourceStopped?.Invoke(this, new EventArgs());
		}
	}

	public class MetaData
	{
		/// <summary>Defaults to: invoker.DbId - Can be set if the owner of a song differs from the invoker.</summary>
		public ulong? ResourceOwnerDbId { get; set; } = null;
		/// <summary>Defaults to: AudioFramwork.Defaultvolume - Overrides the starting volume.</summary>
		public int? Volume { get; set; } = null;
		/// <summary>Default: false - Indicates whether the song has been requested from a playlist.</summary>
		public bool FromPlaylist { get; set; } = false;
	}

	public class PlayInfoEventArgs : EventArgs
	{
		public ClientData Invoker { get; }
		public PlayResource PlayResource { get; }
		public AudioResource ResourceData => PlayResource.BaseData;
		public MetaData MetaData { get; }

		public PlayInfoEventArgs(ClientData invoker, PlayResource playResource, MetaData meta)
		{
			Invoker = invoker;
			PlayResource = playResource;
			MetaData = meta;
		}
	}
}
