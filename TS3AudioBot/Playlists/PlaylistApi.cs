using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3AudioBot.Playlists
{
	public class PlaylistItemGetData
	{
		// Optional, useful when adding a single element to a list
		// public int? Index { get; set; }
		public string Title { get; set; }
		public string AudioType { get; set; }
		// Link
		// AlbumCover

		public static PlaylistItemGetData FromResource(ResourceFactories.AudioResource resource)
		{
			return new PlaylistItemGetData
			{
				Title = resource.ResourceTitle,
				AudioType = resource.AudioType,
			};
		}
	}

	public class PlaylistItemSetData
	{
		public int Index { get; set; }
		public string Title { get; set; }
	}
}
