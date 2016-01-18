using System;

namespace TS3AudioBot.ResourceFactories
{
	interface IResourceFactory : IDisposable
	{
		AudioType FactoryFor { get; }

		bool MatchLink(string uri);
		RResultCode GetRessource(string url, out AudioResource ressource);
		RResultCode GetRessourceById(string id, string name, out AudioResource ressource);
		string RestoreLink(string id);
		void PostProcess(PlayData data, out bool abortPlay);
	}
}
