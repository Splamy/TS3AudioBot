using System;
using TeamSpeak3QueryApi.Net.Specialized.Responses;

namespace TS3AudioBot.RessourceFactories
{
	abstract class AudioRessource
	{
		public abstract AudioType AudioType { get; }
		public string RessourceTitle { get; private set; }
		public string RessourceURL { get; private set; }

		public int Volume { get; set; }
		public bool Enqueue { get; set; }
		public GetClientsInfo InvokingUser { get; set; }

		protected AudioRessource(string ressourceURL, string ressourceTitle)
		{
			RessourceURL = ressourceURL;
			RessourceTitle = ressourceTitle;

			Volume = -1;
			Enqueue = false;
			InvokingUser = null;
		}

		public abstract bool Play(Action<string> setMedia);

		public override string ToString()
		{
			return string.Format("{0} (@{1}) - {2}", RessourceTitle, Volume, AudioType);
		}
	}
}
