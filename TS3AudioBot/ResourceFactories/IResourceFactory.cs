namespace TS3AudioBot.ResourceFactories
{
	using System;
	using Helper;

	public interface IResourceFactory : IDisposable
	{
		AudioType FactoryFor { get; }

		bool MatchLink(string uri);
		R<PlayResource> GetResource(string url);
		R<PlayResource> GetResourceById(string id, string name);
		string RestoreLink(string id);
		R<PlayResource> PostProcess(PlayData data);
	}
}
