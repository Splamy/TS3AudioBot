using System;
using System.Net;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace TS3AudioBot.RessourceFactories
{
	class SoundcloudFactory : IRessourceFactory
	{
		private WebClient wc;
		private JavaScriptSerializer jsonParser;

		public AudioType FactoryFor { get { return AudioType.MediaLink; } }
		public string SoundcloudClientID { get; private set; }

		public SoundcloudFactory()
		{
			wc = new WebClient();
			jsonParser = new JavaScriptSerializer();
			SoundcloudClientID = "a9dd3403f858e105d7e266edc162a0c5";
		}

		public RResultCode GetRessource(string link, out AudioRessource ressource)
		{
			string jsonResponse;
			try
			{
				jsonResponse = wc.DownloadString(
					string.Format("https://api.soundcloud.com/resolve.json?url={0}&client_id={1}",
					Uri.EscapeUriString(link),
					SoundcloudClientID));
			}
			catch (WebException)
			{
				ressource = null;
				return RResultCode.ScInvalidLink;
			}

			var parsedDict = (Dictionary<string, object>)jsonParser.DeserializeObject(jsonResponse);
			int id = (int)parsedDict["id"];
			string title = (string)parsedDict["title"];
			return GetRessourceById(id.ToString(), title, out ressource);
		}

		public RResultCode GetRessourceById(string id, string name, out AudioRessource ressource)
		{
			string finalRequest = string.Format("https://api.soundcloud.com/tracks/{0}/stream?client_id={1}", id, SoundcloudClientID);
			ressource = new SoundcloudRessource(id, name, finalRequest);
			return RResultCode.Success;
		}

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

	class SoundcloudRessource : AudioRessource
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
