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
	using ResourceFactories;
	using Helper;

	public class PlayManager
	{
		private MainBot botParent;
		private AudioFramework audioFramework => botParent.AudioFramework;
		private PlaylistManager playlistManager => botParent.PlaylistManager;
		private ResourceFactoryManager resourceFactoryManager => botParent.FactoryManager;

		// TODO add all events here

		public PlayManager(MainBot parent)
		{
			botParent = parent;
		}

		/// <summary>Playes the passed <see cref="PlayData.PlayResource"/></summary>
		/// <param name="data">The building parameters for the resource.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public R Play(PlayData playData)
		{
			var result = resourceFactoryManager.Load(playData);
			if (!result)
				return result.Message;
			return Play(playData, result.Value);
		}

		/// <summary>Playes the passed <see cref="PlayData.PlayResource"/></summary>
		/// <param name="data">The building parameters for the resource.</param>
		/// <param name="audioType">The associated <see cref="AudioType"/> to a factory.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public R Play(PlayData playData, AudioType audioType)
		{
			var result = resourceFactoryManager.Load(playData, audioType);
			if (!result)
				return result.Message;
			return Play(playData, result.Value);
		}

		private R Play(PlayData playData, PlayResource playRes)
		{
			if (playData.Enqueue && audioFramework.IsPlaying)
			{
				playlistManager.AddToPlaylist(playData);
				return R.OkR;
			}

			if (playData.UsePostProcess)
			{
				var result = resourceFactoryManager.PostProcess(playData);
				if (!result)
					return result.Message;
				else
					playData.PlayResource = result.Value;
			}
			else
			{
				playData.PlayResource = playRes;
			}

			return audioFramework.StartResource(playData);
		}
	}
}
