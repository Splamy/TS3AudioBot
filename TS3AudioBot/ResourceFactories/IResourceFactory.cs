using System;

namespace TS3AudioBot.ResourceFactories
{
	interface IResourceFactory : IDisposable
	{
		AudioType FactoryFor { get; }

		bool MatchLink(string uri);
		RResultCode GetResource(string url, out AudioResource resource);
		RResultCode GetResourceById(string id, string name, out AudioResource resource);
		string RestoreLink(string id);
		void PostProcess(PlayData data, out bool abortPlay);
	}
}
