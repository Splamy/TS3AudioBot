namespace TS3AudioBot.ResourceFactories
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3AudioBot.Helper;
	using TS3AudioBot.History;

	public sealed class ResourceFactoryManager : MarshalByRefObject, IDisposable
	{
		public IResourceFactory DefaultFactorty { get; internal set; }
		private IList<IResourceFactory> factories;
		private AudioFramework audioFramework;

		public ResourceFactoryManager(AudioFramework audioFramework)
		{
			factories = new List<IResourceFactory>();
			this.audioFramework = audioFramework;
		}

		public string LoadAndPlay(PlayData data)
		{
			string netlinkurl = TextUtil.ExtractUrlFromBB(data.Message);
			IResourceFactory factory = GetFactoryFor(netlinkurl);
			return LoadAndPlay(factory, data);
		}

		public string LoadAndPlay(AudioType audioType, PlayData data)
		{
			var factory = GetFactoryFor(audioType);
			return LoadAndPlay(factory, data);
		}

		private string LoadAndPlay(IResourceFactory factory, PlayData data)
		{
			if (data.Resource == null)
			{
				string netlinkurl = TextUtil.ExtractUrlFromBB(data.Message);

				AudioResource resource;
				RResultCode result = factory.GetResource(netlinkurl, out resource);
				if (result != RResultCode.Success)
					return $"Could not play ({result})";
				data.Resource = resource;
			}
			return PostProcessStart(factory, data);
		}

		public string RestoreAndPlay(AudioLogEntry logEntry, PlayData data)
		{
			var factory = GetFactoryFor(logEntry.AudioType);

			if (data.Resource == null)
			{
				AudioResource resource;
				RResultCode result = factory.GetResourceById(logEntry.ResourceId, logEntry.ResourceTitle, out resource);
				if (result != RResultCode.Success)
					return $"Could not restore ({result})";
				data.Resource = resource;
			}
			return PostProcessStart(factory, data);
		}

		private string PostProcessStart(IResourceFactory factory, PlayData data)
		{
			bool abortPlay;
			factory.PostProcess(data, out abortPlay);
			return abortPlay ? null : Play(data);
		}

		public string Play(PlayData data)
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
					return $"The resource could not be played ({result}).";
			}
			return null;
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
