using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TS3AudioBot.Helper;

namespace TS3AudioBot.RessourceFactories
{
	class RessourceFactoryManager : IDisposable
	{
		public IRessourceFactory DefaultFactorty { get; set; }
		private IList<IRessourceFactory> factories;
		private AudioFramework audioFramework;

		public RessourceFactoryManager(AudioFramework audioFramework)
		{
			this.audioFramework = audioFramework;
		}

		public void LoadAndPlay(PlayData data)
		{
			string netlinkurl = TextUtil.ExtractUrlFromBB(data.Message);
			IRessourceFactory factory = GetFactoryFor(netlinkurl);
			LoadAndPlay(factory, data);
		}

		public void LoadAndPlay(AudioType audioType, PlayData data)
		{
			IRessourceFactory factory = factories.SingleOrDefault(f => f.FactoryFor == audioType);
			LoadAndPlay(factory, data);
		}

		private void LoadAndPlay(IRessourceFactory factory, PlayData data)
		{
			if (data.Ressource == null)
			{
				string netlinkurl = TextUtil.ExtractUrlFromBB(data.Message);

				AudioRessource ressource;
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
			IRessourceFactory factory = factories.SingleOrDefault(f => f.FactoryFor == logEntry.AudioType);

			AudioRessource ressource;
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
			data.Ressource.Enqueue = data.Enqueue;
			var result = audioFramework.StartRessource(data.Ressource, data.Invoker);
			if (result != AudioResultCode.Success)
				data.Session.Write(string.Format("The ressource could not be played ({0}).", result));
		}

		private IRessourceFactory GetFactoryFor(string uri)
		{
			foreach (var fac in factories)
				if (fac.MatchLink(uri)) return fac;
			return DefaultFactorty;
		}

		public void AddFactory(IRessourceFactory factory)
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
