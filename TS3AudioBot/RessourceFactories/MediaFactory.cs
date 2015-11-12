using System;
using System.Net;
using System.IO;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;

namespace TS3AudioBot.RessourceFactories
{
	class MediaFactory : IRessourceFactory
	{
		public RResultCode GetRessource(string uri, out AudioRessource ressource)
		{
			var result = ValidateUri(uri);
			ressource = new MediaRessource(uri, uri, result);
			// brach here if we have a final ErrorCode
			if (result == RResultCode.MediaNoWebResponse)
				return result;
			// or branch here if we can't figure out for sure
			// and pass it to the user
			else
				return RResultCode.Success;
		}

		private RResultCode ValidateUri(string uri)
		{
			Uri uriResult;
			if (!Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out uriResult))
				return RResultCode.MediaInvalidUri;
			string scheme;
			try
			{
				scheme = uriResult.Scheme;
				if (scheme == Uri.UriSchemeHttp
					|| scheme == Uri.UriSchemeHttps
					|| scheme == Uri.UriSchemeFtp)
					return ValidateWeb(uri);
				else if (uriResult.Scheme == Uri.UriSchemeFile)
					return ValidateFile(uri);
				else
					return RResultCode.MediaUnknownUri;
			}
			catch
			{
				return ValidateFile(uri);
			}
		}

		private RResultCode ValidateWeb(string link)
		{
			var request = WebRequest.Create(link);
			if (request.Method == "GET")
				request.Method = "HEAD";
			try
			{
				request.GetResponse();
				return RResultCode.Success;
			}
			catch
			{
				return RResultCode.MediaNoWebResponse;
			}
		}

		private RResultCode ValidateFile(string path)
		{
			if (File.Exists(path))
				return RResultCode.Success;
			else
				return RResultCode.MediaFileNotFound;
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

		public RResultCode InternalResultCode { get; private set; }

		public MediaRessource(string path, string name, RResultCode internalRC)
			: base(path, name)
		{
			InternalResultCode = internalRC;
		}

		public override bool Play(Action<string> setMedia)
		{
			setMedia(RessourceURL);
			return true;
		}
	}
}
