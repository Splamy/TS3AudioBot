using System;
using System.Net;
using System.IO;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;
using TS3AudioBot.Helper.AudioTags;

namespace TS3AudioBot.RessourceFactories
{
	class MediaFactory : IRessourceFactory
	{
		public AudioType FactoryFor { get { return AudioType.MediaLink; } }

		public RResultCode GetRessource(string uri, out AudioRessource ressource)
		{
			return GetRessourceById(uri, null, out ressource);
		}

		public RResultCode GetRessourceById(string id, string name, out AudioRessource ressource)
		{
			Stream peekStream;
			var result = ValidateUri(id, out peekStream);

			// brach here if we have a final ErrorCode
			if (result == RResultCode.MediaNoWebResponse)
			{
				ressource = null;
				return result;
			}
			// or branch here on success or if we can't figure out for sure
			// and pass it to the user
			else
			{
				if (string.IsNullOrEmpty(name))
				{
					if (peekStream != null)
					{
						try { name = AudioTagReader.GetTitle(peekStream); }
						catch { name = id; }
					}
					else
						name = id;
				}

				ressource = new MediaRessource(id, name, id, result);
				return RResultCode.Success;
			}
		}

		private RResultCode ValidateUri(string uri, out Stream stream)
		{
			Uri uriResult;
			if (!Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out uriResult))
			{
				stream = null;
				return RResultCode.MediaInvalidUri;
			}
			string scheme;
			try
			{
				scheme = uriResult.Scheme;
				if (scheme == Uri.UriSchemeHttp
					|| scheme == Uri.UriSchemeHttps
					|| scheme == Uri.UriSchemeFtp)
					return ValidateWeb(uri, out stream);
				else if (uriResult.Scheme == Uri.UriSchemeFile)
					return ValidateFile(uri, out stream);
				else
				{
					stream = null;
					return RResultCode.MediaUnknownUri;
				}
			}
			catch
			{
				return ValidateFile(uri, out stream);
			}
		}

		private RResultCode ValidateWeb(string link, out Stream stream)
		{
			var request = WebRequest.Create(link);
			//if (request.Method == "GET")
			//	request.Method = "HEAD";
			try
			{
				var response = request.GetResponse();
				stream = response.GetResponseStream();
				return RResultCode.Success;
			}
			catch
			{
				stream = null;
				return RResultCode.MediaNoWebResponse;
			}
		}

		private RResultCode ValidateFile(string path, out Stream stream)
		{
			if (File.Exists(path))
			{
				try { stream = new FileStream(path, FileMode.Open, FileAccess.Read); }
				catch { stream = null; }
				return RResultCode.Success;
			}
			else
			{
				stream = null;
				return RResultCode.MediaFileNotFound;
			}
		}

		public void PostProcess(PlayData data, out bool abortPlay)
		{
			MediaRessource mediaRessource = (MediaRessource)data.Ressource;
			if (mediaRessource.InternalResultCode == RResultCode.Success)
			{
				abortPlay = false;
			}
			else
			{
				abortPlay = true;
				data.Session.Write(
					string.Format("This uri might be invalid ({0}), do you want to start anyway?",
						mediaRessource.InternalResultCode));
				data.Session.UserRessource = data;
				data.Session.SetResponse(ResponseValidation, null, false);
			}
		}

		private static bool ResponseValidation(BotSession session, TextMessage tm, bool isAdmin)
		{
			string[] command = tm.Message.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (command[0] == "!y")
			{
				PlayData data = session.UserRessource;
				session.Bot.Play(data);
				return true;
			}
			else if (command[0] == "!n")
			{
				session.UserRessource = null;
				return true;
			}
			else
				return false;
		}

		public void Dispose()
		{

		}
	}

	class MediaRessource : AudioRessource
	{
		public override AudioType AudioType { get { return AudioType.MediaLink; } }

		public string RessourceURL { get; private set; }
		public RResultCode InternalResultCode { get; private set; }

		public MediaRessource(string id, string name, string url, RResultCode internalRC)
			: base(id, name)
		{
			RessourceURL = url;
			InternalResultCode = internalRC;
		}

		public override string Play()
		{
			return RessourceURL;
		}
	}
}
