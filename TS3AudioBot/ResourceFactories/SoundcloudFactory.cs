namespace TS3AudioBot.ResourceFactories
{
	using System;
	using System.Collections.Generic;
	using System.Net;
	using System.Text.RegularExpressions;
	using System.Web.Script.Serialization;

	public class SoundcloudFactory : IResourceFactory
	{
		private WebClient wc;
		private JavaScriptSerializer jsonParser;

		public AudioType FactoryFor => AudioType.Soundcloud;
		public string SoundcloudClientID { get; private set; }

		public SoundcloudFactory()
		{
			wc = new WebClient();
			jsonParser = new JavaScriptSerializer();
			SoundcloudClientID = "a9dd3403f858e105d7e266edc162a0c5";
		}

		public bool MatchLink(string link) => Regex.IsMatch(link, @"^https?\:\/\/(www\.)?soundcloud\.");

		public RResultCode GetResource(string link, out AudioResource resource)
		{
			string jsonResponse;
			try
			{
				jsonResponse = wc.DownloadString($"https://api.soundcloud.com/resolve.json?url={Uri.EscapeUriString(link)}&client_id={SoundcloudClientID}");
			}
			catch (WebException)
			{
				resource = null;
				return RResultCode.ScInvalidLink;
			}

			var parsedDict = ParseJson(jsonResponse);
			int id = (int)parsedDict["id"];
			string title = (string)parsedDict["title"];
			return GetResourceById(id.ToString(), title, out resource);
		}

		public RResultCode GetResourceById(string id, string name, out AudioResource resource)
		{
			string finalRequest = $"https://api.soundcloud.com/tracks/{id}/stream?client_id={SoundcloudClientID}";
			resource = new SoundcloudResource(id, name, finalRequest);
			return RResultCode.Success;
		}

		public string RestoreLink(string id)
		{
			string jsonResponse = $"https://api.soundcloud.com/tracks/{id}?client_id={SoundcloudClientID}";
			var parsedDict = ParseJson(jsonResponse);
			return (string)parsedDict["permalink_url"];
		}

		private Dictionary<string, object> ParseJson(string jsonResponse) => (Dictionary<string, object>)jsonParser.DeserializeObject(jsonResponse);

		public void PostProcess(PlayData data, out bool abortPlay)
		{
			abortPlay = false;
		}

		public void Dispose()
		{
			if (wc != null)
			{
				wc.Dispose();
				wc = null;
			}
		}
	}

	class SoundcloudResource : AudioResource
	{
		public override AudioType AudioType => AudioType.Soundcloud;

		public string ResourceURL { get; private set; }

		public SoundcloudResource(string id, string name, string url)
			: base(id, name)
		{
			ResourceURL = url;
		}

		public override string Play()
		{
			return ResourceURL;
		}
	}
}
