using System;

namespace TS3AudioBot.RessourceFactories
{
	class MediaFactory : IRessourceFactory
	{
		public bool GetRessource(string url, out AudioRessource ressource)
		{
			ressource = new MediaRessource(url, url);
			return true;
		}

		public void PostProcess(PlayData data, out bool abortPlay)
		{
			abortPlay = false;
		}

		public void Dispose()
		{

		}
	}

	class MediaRessource : AudioRessource
	{
		public override AudioType AudioType { get { return AudioType.MediaLink; } }

		public MediaRessource(string path, string name)
			: base(path, name)
		{ }

		public override bool Play(Action<string> setMedia)
		{
			setMedia(RessourceURL);
			return true;
		}
	}
}
