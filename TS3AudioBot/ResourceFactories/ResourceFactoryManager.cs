namespace TS3AudioBot.ResourceFactories
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3AudioBot.Helper;
	using TS3AudioBot.History;

	public sealed class ResourceFactoryManager : IDisposable
	{
		public IResourceFactory DefaultFactorty { get; internal set; }
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
			var factory = GetFactoryFor(audioType);
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
					data.Session.Write($"Could not play ({result})");
					return;
				}
				data.Resource = resource;
			}

			bool abortPlay;
			factory.PostProcess(data, out abortPlay);
			if (!abortPlay)
				Play(data);
		}

		internal void RestoreAndPlay(AudioLogEntry logEntry, PlayData data)
		{
			var factory = GetFactoryFor(logEntry.AudioType);

			if (data.Resource == null)
			{
				AudioResource resource;
				RResultCode result = factory.GetResourceById(logEntry.ResourceId, logEntry.ResourceTitle, out resource);
				if (result != RResultCode.Success)
				{
					data.Session.Write($"Could not restore ({result})");
					return;
				}
				data.Resource = resource;
			}

			bool abortPlay;
			factory.PostProcess(data, out abortPlay);
			if (!abortPlay)
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
					data.Session.Write($"The resource could not be played ({result}).");
			}
		}

		private IResourceFactory GetFactoryFor(AudioType audioType)
		{
			foreach (var fac in factories)
				if (fac.FactoryFor == audioType) return fac;
			return DefaultFactorty;
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
			IResourceFactory factory = GetFactoryFor(res.AudioType);
			return factory.RestoreLink(res.ResourceId);
		}

		public void Dispose()
		{
			foreach (var fac in factories)
				fac.Dispose();
		}
	}
}
