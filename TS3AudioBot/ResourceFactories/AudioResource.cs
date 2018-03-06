// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.ResourceFactories
{
	public class PlayResource
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
		public string AudioType { get; set; }
		/// <summary>An identifier to create the song. This id is uniqe among all resources with the same resource type string of a factory.</summary>
		public string ResourceId { get; set; }
		/// <summary>The display title.</summary>
		public string ResourceTitle { get; set; }
		/// <summary>An identifier wich is unique among all <see cref="AudioResource"/> and resource type string of a factory.</summary>
		public string UniqueId => ResourceId + AudioType;

		public AudioResource() { }

		public AudioResource(string resourceId, string resourceTitle, string audioType)
		{
			ResourceId = resourceId;
			ResourceTitle = resourceTitle;
			AudioType = audioType;
		}

		public AudioResource WithName(string newName) => new AudioResource(ResourceId, newName, AudioType);

		public override bool Equals(object obj)
		{
			if (!(obj is AudioResource other))
				return false;

			return AudioType == other.AudioType
				&& ResourceId == other.ResourceId;
		}

		public override int GetHashCode()
		{
			int hash = 0x7FFFF + AudioType.GetHashCode();
			hash = (hash * 0x1FFFF) + ResourceId.GetHashCode();
			return hash;
		}

		public override string ToString()
		{
			return $"{AudioType} ID:{ResourceId}";
		}
	}
}
