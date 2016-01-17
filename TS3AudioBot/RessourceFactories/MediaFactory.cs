using System;
using System.Net;
using System.IO;
using TS3Query.Messages;
using TS3AudioBot.Helper.AudioTags;
using TS3AudioBot.Helper;

namespace TS3AudioBot.RessourceFactories
{
	class MediaFactory : IRessourceFactory
	{
		public AudioType FactoryFor => AudioType.MediaLink;

		public bool MatchLink(string uri) => true;

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
						name = AudioTagReader.GetTitle(peekStream);
						name = string.IsNullOrWhiteSpace(name) ? id : name;
					}
					else
						name = id;
				}

				ressource = new MediaRessource(id, name, id, result);
				return RResultCode.Success;
			}
		}

		public string RestoreLink(string id) => id;

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
			catch (InvalidOperationException) { return ValidateFile(uri, out stream); }
		}

		private RResultCode ValidateWeb(string link, out Stream stream)
		{
			stream = null;
			var request = WebRequest.Create(link);
			//if (request.Method == "GET")
			//	request.Method = "HEAD";
			try
			{
				var response = request.GetResponse();
				stream = response.GetResponseStream();
				return RResultCode.Success;
			}
			catch (WebException) { return RResultCode.MediaNoWebResponse; }
			catch (ProtocolViolationException) { return RResultCode.MediaNoWebResponse; }
		}

		private RResultCode ValidateFile(string path, out Stream stream)
		{
			if (File.Exists(path))
			{
				try { stream = File.Open(path, FileMode.Open, FileAccess.Read); }
				catch (Exception) { stream = null; }
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
			Answer answer = TextUtil.GetAnswer(tm.Message);
			if (answer == Answer.Yes)
			{
				PlayData data = session.UserRessource;
				session.Bot.FactoryManager.Play(data);
			}
			else if (answer == Answer.No)
			{
				session.UserRessource = null;
			}
			return answer != Answer.Unknown;
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
