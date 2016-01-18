using System;
using System.Net;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TS3AudioBot.ResourceFactories
{
	class SoundcloudFactory : IResourceFactory
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

		public RResultCode GetRessource(string link, out AudioResource ressource)
		{
			string jsonResponse;
			try
			{
				jsonResponse = wc.DownloadString($"https://api.soundcloud.com/resolve.json?url={Uri.EscapeUriString(link)}&client_id={SoundcloudClientID}");
			}
			catch (WebException)
			{
				ressource = null;
				return RResultCode.ScInvalidLink;
			}

			var parsedDict = ParseJson(jsonResponse);
			int id = (int)parsedDict["id"];
			string title = (string)parsedDict["title"];
			return GetRessourceById(id.ToString(), title, out ressource);
		}

		public RResultCode GetRessourceById(string id, string name, out AudioResource ressource)
		{
			string finalRequest = string.Format("https://api.soundcloud.com/tracks/{0}/stream?client_id={1}", id, SoundcloudClientID);
			ressource = new SoundcloudRessource(id, name, finalRequest);
			return RResultCode.Success;
		}

		public string RestoreLink(string id)
		{
			string jsonResponse = string.Format("https://api.soundcloud.com/tracks/{0}?client_id={1}", id, SoundcloudClientID);
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

	class SoundcloudRessource : AudioResource
	{
		public override AudioType AudioType { get { return AudioType.Soundcloud; } }

		public string RessourceURL { get; private set; }

		public SoundcloudRessource(string id, string name, string url)
			: base(id, name)
		{
			RessourceURL = url;
		}

		public override string Play()
		{
			return RessourceURL;
		}
	}
}
