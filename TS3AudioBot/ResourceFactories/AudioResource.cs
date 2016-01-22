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
			return string.Format("{0}: {1} (ID:{2})", AudioType, ResourceTitle, ResourceId);
		}
	}
}
