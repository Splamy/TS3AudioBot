using System;

namespace TS3AudioBot
{
	interface IRessourceFactory : IDisposable
	{
		bool GetRessource(string url, out AudioRessource ressource);
		void PostProcess(PlayData data, out bool abortPlay);
	}
}
