using System;

namespace TS3AudioBot.RessourceFactories
{
	interface IRessourceFactory : IDisposable
	{
		AudioType FactoryFor { get; }

		bool MatchLink(string uri);
		RResultCode GetRessource(string url, out AudioRessource ressource);
		RResultCode GetRessourceById(string id, string name, out AudioRessource ressource);
		string RestoreLink(string id);
		void PostProcess(PlayData data, out bool abortPlay);
	}
}
