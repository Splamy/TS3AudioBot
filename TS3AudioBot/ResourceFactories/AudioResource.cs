namespace TS3AudioBot.ResourceFactories
{
	public abstract class AudioResource
	{
		public abstract AudioType AudioType { get; }
		public string ResourceTitle { get; protected set; }
		public string ResourceId { get; private set; }

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
	}
}
