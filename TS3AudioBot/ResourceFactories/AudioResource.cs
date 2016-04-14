namespace TS3AudioBot.ResourceFactories
{
	using System;

	public abstract class AudioResource : MarshalByRefObject
	{
		/// <summary>The resource type.</summary>
		public abstract AudioType AudioType { get; }
		/// <summary>The display title.</summary>
		public string ResourceTitle { get; set; }
		/// <summary>An identifier to create the song. This id is uniqe among same <see cref="AudioType"/> resources.</summary>
		public string ResourceId { get; }
		/// <summary>An identifier wich is unique among all <see cref="AudioResource"/> and <see cref="AudioType"/>.</summary>
		public string UniqueId => ResourceId + AudioType.ToString();
 
		protected AudioResource(string resourceId, string resourceTitle)
		{
			ResourceTitle = resourceTitle;
			ResourceId = resourceId;
		}

		public abstract string Play();

		public override string ToString()
		{
			return $"{AudioType}: {ResourceTitle} (ID:{ResourceId})";
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			var other = obj as AudioResource;
			if (other == null)
				return false;

			return AudioType == other.AudioType
				&& ResourceTitle == other.ResourceTitle
				&& ResourceId == other.ResourceId;
		}

		public override int GetHashCode()
		{
			int hash = 0x7FFFF + (int)AudioType;
			hash = (hash * 0x1FFFF) + ResourceTitle.GetHashCode();
			hash = (hash * 0x1FFFF) + ResourceId.GetHashCode();
			return hash;
		}
	}
}
