using System;
using System.Net;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace TS3AudioBot
{
	class SoundcloudFramework : IRessourceFactory
	{
		private WebClient wc;
		private JavaScriptSerializer jsonParser;

		public string SoundcloudClientID { get; private set; }

		public SoundcloudFramework()
		{
			wc = new WebClient();
			jsonParser = new JavaScriptSerializer();
			SoundcloudClientID = "a9dd3403f858e105d7e266edc162a0c5";
		}

		public bool GetRessource(string link, out AudioRessource ressource)
		{
			string jsonResponse;
			try
			{
				jsonResponse = wc.DownloadString(
					string.Format("https://api.soundcloud.com/resolve.json?url={0}&client_id={1}",
					Uri.EscapeUriString(link),
					SoundcloudClientID));
			}
			catch
			{
				ressource = null;
				return false;
			}

			var parsedDict = (Dictionary<string, object>)jsonParser.DeserializeObject(jsonResponse);
			int id = (int)parsedDict["id"];
			string title = (string)parsedDict["title"];

			string finalRequest = string.Format("https://api.soundcloud.com/tracks/{0}/stream?client_id={1}", id, SoundcloudClientID);
			ressource = new SoundcloudRessource(finalRequest, title);
			return true;
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

		public SoundcloudRessource(string path, string name)
			: base(path, name)
		{ }

		public override bool Play(Action<string> setMedia)
		{
			setMedia(RessourceURL);
			return true;
		}
	}
}
