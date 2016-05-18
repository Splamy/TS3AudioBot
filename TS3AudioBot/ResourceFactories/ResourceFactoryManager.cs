namespace TS3AudioBot.ResourceFactories
{
	using System;
	using System.Collections.Generic;
	using Helper;
	using History;

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

		public R LoadAndPlay(PlayData data)
		{
			string netlinkurl = TextUtil.ExtractUrlFromBB(data.Message);
			IResourceFactory factory = GetFactoryFor(netlinkurl);
			return LoadAndPlay(factory, data);
		}

		public R LoadAndPlay(AudioType audioType, PlayData data)
		{
			var factory = GetFactoryFor(audioType);
			return LoadAndPlay(factory, data);
		}

		private R LoadAndPlay(IResourceFactory factory, PlayData data)
		{
			if (data.ResourceData == null)
			{
				string netlinkurl = TextUtil.ExtractUrlFromBB(data.Message);

				var result = factory.GetResource(netlinkurl);
				if (!result)
					return $"Could not play ({result.Message})";
				data.PlayResource = result.Value;
			}
			return PostProcessAndStartInternal(factory, data);
		}

		public R<PlayResource> Restore(AudioResource resource)
			=> RestoreInternal(GetFactoryFor(resource.AudioType), resource);

		private R<PlayResource> RestoreInternal(IResourceFactory factory, AudioResource resource)
		{
			var result = factory.GetResourceById(resource.ResourceId, resource.ResourceTitle);
			if (!result)
				return $"Could not restore ({result.Message})";
			return result;
		}

		public R RestoreAndPlay(AudioLogEntry logEntry, PlayData data)
		{
			var factory = GetFactoryFor(logEntry.AudioType);
			var result = RestoreInternal(factory, logEntry);
			if (!result) return result.Message;
			data.PlayResource = result.Value;
			return PostProcessAndStartInternal(factory, data);
		}

		public R PostProcessAndStart(PlayData data)
			=> PostProcessAndStartInternal(GetFactoryFor(data.ResourceData.AudioType), data);

		private R PostProcessAndStartInternal(IResourceFactory factory, PlayData data)
		{
			var result = factory.PostProcess(data);
			if (!result)
				return result.Message;
			else
				return Play(data);
		}

		public R Play(PlayData data)
		{
			if (data.Enqueue && audioFramework.IsPlaying)
			{
				audioFramework.PlaylistManager.AddToPlaylist(data);
				return R.OkR;
			}
			else
			{
				return audioFramework.StartResource(data);
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

		public string RestoreLink(PlayData data) => RestoreLink(data.ResourceData);
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
