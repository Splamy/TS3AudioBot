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
	using System;

	public sealed class PlayResource
	{
		public AudioResource BaseData { get; }
		public string PlayUri { get; }

		public PlayResource(string uri, AudioResource baseData)
		{
			BaseData = baseData;
			PlayUri = uri;
		}

		public override string ToString() => BaseData.ToString();
	}

	public class AudioResource
	{
		/// <summary>The resource type.</summary>
		public AudioType AudioType { get; set; }
		/// <summary>An identifier to create the song. This id is uniqe among same <see cref="TS3AudioBot.AudioType"/> resources.</summary>
		public string ResourceId { get; set;  }
		/// <summary>The display title.</summary>
		public string ResourceTitle { get; set; }
		/// <summary>An identifier wich is unique among all <see cref="AudioResource"/> and <see cref="TS3AudioBot.AudioType"/>.</summary>
		public string UniqueId => ResourceId + AudioType.ToString();

		public AudioResource() { }

		public AudioResource(string resourceId, string resourceTitle, AudioType type)
		{
			ResourceId = resourceId;
			ResourceTitle = resourceTitle;
			AudioType = type;
		}

		public AudioResource(AudioResource copyResource)
		{
			ResourceId = copyResource.ResourceId;
			ResourceTitle = copyResource.ResourceTitle;
			AudioType = copyResource.AudioType;
		}

		public AudioResource WithName(string newName) => new AudioResource(ResourceId, newName, AudioType);

		public override bool Equals(object obj)
		{
			var other = obj as AudioResource;
			if (other == null)
				return false;

			return AudioType == other.AudioType
				&& ResourceId == other.ResourceId;
		}

		public override int GetHashCode()
		{
			int hash = 0x7FFFF + (int)AudioType;
			hash = (hash * 0x1FFFF) + ResourceId.GetHashCode();
			return hash;
		}

		public override string ToString()
		{
			return $"{AudioType} ID:{ResourceId}";
		}
	}
}
