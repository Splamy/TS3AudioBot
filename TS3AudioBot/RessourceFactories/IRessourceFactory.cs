using System;

namespace TS3AudioBot.RessourceFactories
{
	interface IRessourceFactory : IDisposable
	{
		bool GetRessource(string url, out AudioRessource ressource);
		void PostProcess(PlayData data, out bool abortPlay);
	}
}
