namespace TS3AudioBot.ResourceFactories
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3AudioBot.Helper;

	class ResourceFactoryManager : IDisposable
	{
		public IResourceFactory DefaultFactorty { get; set; }
		private IList<IResourceFactory> factories;
		private AudioFramework audioFramework;

		public ResourceFactoryManager(AudioFramework audioFramework)
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
			if (data.Resource == null)
			{
				string netlinkurl = TextUtil.ExtractUrlFromBB(data.Message);

				AudioResource resource;
				RResultCode result = factory.GetResource(netlinkurl, out resource);
				if (result != RResultCode.Success)
				{
					data.Session.Write(string.Format("Could not play ({0})", result));
					return;
				}
				data.Resource = resource;
			}

			bool abortPlay;
			factory.PostProcess(data, out abortPlay);
			if (!abortPlay)
				Play(data);
		}

		public void RestoreAndPlay(AudioLogEntry logEntry, PlayData data)
		{
			IResourceFactory factory = factories.SingleOrDefault(f => f.FactoryFor == logEntry.AudioType);

			AudioResource resource;
			RResultCode result = factory.GetResourceById(logEntry.ResourceId, logEntry.Title, out resource);
			if (result != RResultCode.Success)
			{
				data.Session.Write(string.Format("Could not restore ({0})", result));
				return;
			}
			data.Resource = resource;

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
				var result = audioFramework.StartResource(data);
				if (result != AudioResultCode.Success)
					data.Session.Write(string.Format("The resource could not be played ({0}).", result));
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

		public string RestoreLink(PlayData data) => RestoreLink(data.Resource);
		public string RestoreLink(AudioResource res)
		{
			IResourceFactory factory = factories.SingleOrDefault(f => f.FactoryFor == res.AudioType);
			return factory.RestoreLink(res.ResourceId);
		}

		public void Dispose()
		{
			foreach (var fac in factories)
				fac.Dispose();
		}
	}
}
