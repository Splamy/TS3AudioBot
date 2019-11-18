// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Newtonsoft.Json;
using System.Collections.Generic;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem.CommandResults;

namespace TS3AudioBot.ResourceFactories
{
	public class PlayResource
	{
		public AudioResource BaseData { get; }
		public string PlayUri { get; }
		public MetaData Meta { get; set; }

		public PlayResource(string uri, AudioResource baseData, MetaData meta = null)
		{
			BaseData = baseData;
			PlayUri = uri;
			Meta = meta;
		}

		public override string ToString() => BaseData.ToString();
	}

	public class AudioResource : IAudioResourceResult
	{
		/// <summary>The resource type.</summary>
		[JsonProperty(PropertyName = "type")]
		public string AudioType { get; set; }
		/// <summary>An identifier to create the song. This id is uniqe among all resources with the same resource type string of a factory.</summary>
		[JsonProperty(PropertyName = "resid")]
		public string ResourceId { get; set; }
		/// <summary>The display title.</summary>
		[JsonProperty(PropertyName = "title")]
		public string ResourceTitle { get; set; }
		/// <summary>Additional data to resolve the link.</summary>
		[JsonProperty(PropertyName = "add", NullValueHandling = NullValueHandling.Ignore)]
		public Dictionary<string, string> AdditionalData { get; set; }
		/// <summary>An identifier wich is unique among all <see cref="AudioResource"/> and resource type string of a factory.</summary>
		[JsonIgnore]
		public string UniqueId => ResourceId + AudioType;
		[JsonIgnore]
		AudioResource IAudioResourceResult.AudioResource => this;

		public AudioResource() { }

		public AudioResource(string resourceId, string resourceTitle, string audioType, Dictionary<string, string> additionalData = null)
		{
			ResourceId = resourceId;
			ResourceTitle = resourceTitle;
			AudioType = audioType;
			AdditionalData = additionalData;
		}

		public AudioResource Add(string key, string value)
		{
			if (AdditionalData == null)
				AdditionalData = new Dictionary<string, string>();
			AdditionalData.Add(key, value);
			return this;
		}

		public string Get(string key)
		{
			if (AdditionalData == null)
				return null;
			return AdditionalData.TryGetValue(key, out var value) ? value : null;
		}

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
