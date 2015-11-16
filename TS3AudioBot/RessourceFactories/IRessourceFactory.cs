using System;

namespace TS3AudioBot.RessourceFactories
{
	interface IRessourceFactory : IDisposable
	{
		AudioType FactoryFor { get; }

		RResultCode GetRessource(string url, out AudioRessource ressource);
		RResultCode GetRessourceById(string id, string name, out AudioRessource ressource);
		void PostProcess(PlayData data, out bool abortPlay);
	}
}
