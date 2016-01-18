using System;
using System.Collections.Generic;
using System.Linq;
using TS3AudioBot.Helper;

namespace TS3AudioBot.ResourceFactories
{
	class RessourceFactoryManager : IDisposable
	{
		public IResourceFactory DefaultFactorty { get; set; }
		private IList<IResourceFactory> factories;
		private AudioFramework audioFramework;

		public RessourceFactoryManager(AudioFramework audioFramework)
		{
			factories = new List<IResourceFactory>();
			this.audioFramework = audioFramework;
		}

		public void LoadAndPlay(PlayData data)
		{
			string netlinkurl = TextUtil.ExtractUrlFromBB(data.Message);
			IResourceFactory factory = GetFactoryFor(netlinkurl);
			LoadAndPlay(factory, data);
		}

		public void LoadAndPlay(AudioType audioType, PlayData data)
		{
			IResourceFactory factory = factories.SingleOrDefault(f => f.FactoryFor == audioType);
			LoadAndPlay(factory, data);
		}

		private void LoadAndPlay(IResourceFactory factory, PlayData data)
		{
			if (data.Ressource == null)
			{
				string netlinkurl = TextUtil.ExtractUrlFromBB(data.Message);

				AudioResource ressource;
				RResultCode result = factory.GetRessource(netlinkurl, out ressource);
				if (result != RResultCode.Success)
				{
					data.Session.Write(string.Format("Could not play ({0})", result));
					return;
				}
				data.Ressource = ressource;
			}

			bool abortPlay;
			factory.PostProcess(data, out abortPlay);
			if (!abortPlay)
				Play(data);
		}

		public void RestoreAndPlay(AudioLogEntry logEntry, PlayData data)
		{
			IResourceFactory factory = factories.SingleOrDefault(f => f.FactoryFor == logEntry.AudioType);

			AudioResource ressource;
			RResultCode result = factory.GetRessourceById(logEntry.RessourceId, logEntry.Title, out ressource);
			if (result != RResultCode.Success)
			{
				data.Session.Write(string.Format("Could not restore ({0})", result));
				return;
			}
			data.Ressource = ressource;

			Play(data);
		}

		public void Play(PlayData data)
		{
			if (data.Enqueue)
			{
				// TODO
				throw new NotImplementedException();
			}
			else
			{
				var result = audioFramework.StartRessource(data);
				if (result != AudioResultCode.Success)
					data.Session.Write(string.Format("The ressource could not be played ({0}).", result));
			}
		}

		private IResourceFactory GetFactoryFor(string uri)
		{
			foreach (var fac in factories)
				if (fac.MatchLink(uri)) return fac;
			return DefaultFactorty;
		}

		public void AddFactory(IResourceFactory factory)
		{
			factories.Add(factory);
		}

		public void Dispose()
		{
			foreach (var fac in factories)
				fac.Dispose();
		}
	}
}
