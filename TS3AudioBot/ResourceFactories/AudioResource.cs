namespace TS3AudioBot.ResourceFactories
{
	using System;

	public abstract class PlayResource
	{
		public AudioResource BaseData;

		protected PlayResource(AudioResource baseData) { BaseData = baseData; }

		public abstract string Play();

		public override string ToString() => BaseData.ToString();
	}

	public class AudioResource : MarshalByRefObject
	{
		/// <summary>The resource type.</summary>
		public AudioType AudioType { get; }
		/// <summary>An identifier to create the song. This id is uniqe among same <see cref="TS3AudioBot.AudioType"/> resources.</summary>
		public string ResourceId { get; }
		/// <summary>The display title.</summary>
		public string ResourceTitle { get; set; }
		/// <summary>An identifier wich is unique among all <see cref="AudioResource"/> and <see cref="TS3AudioBot.AudioType"/>.</summary>
		public string UniqueId => ResourceId + AudioType.ToString();

		public AudioResource(string resourceId, string resourceTitle, AudioType type)
		{
			ResourceId = resourceId;
			ResourceTitle = resourceTitle;
			AudioType = type;
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

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
