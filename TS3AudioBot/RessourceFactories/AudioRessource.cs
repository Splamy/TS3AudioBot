using System;
using TeamSpeak3QueryApi.Net.Specialized.Responses;

namespace TS3AudioBot.RessourceFactories
{
	abstract class AudioRessource
	{
		public abstract AudioType AudioType { get; }
		public string RessourceTitle { get; protected set; }
		public string RessourceId { get; private set; }

		public int Volume { get; set; }
		public bool Enqueue { get; set; }
		public GetClientsInfo InvokingUser { get; set; }

		protected AudioRessource(string ressourceId, string ressourceTitle)
		{
			RessourceTitle = ressourceTitle;
			RessourceId = ressourceId;

			Volume = -1;
			Enqueue = false;
			InvokingUser = null;
		}

		public abstract string Play();

		public override string ToString()
		{
			return string.Format("{0}: {1} (ID:{2})", AudioType, RessourceTitle, RessourceId);
		}
	}
}
