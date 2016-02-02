namespace TS3AudioBot.Helper
{
	using System;
	using System.IO;
	using System.Net;

	static class WebWrapper
	{
		private static TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

		public static bool DownloadString(out string site, Uri link)
		{
			var request = WebRequest.Create(link);
			try
			{
				var response = request.GetResponse();
				var stream = response.GetResponseStream();
				using (var reader = new StreamReader(stream))
				{
					site = reader.ReadToEnd();
					return true;
				}
			}
			catch (WebException)
			{
				site = null;
				return false;
			}
		}

		//if (request.Method == "GET")
		//	request.Method = "HEAD";
		public static ValidateCode CheckResponse(Uri link) => CheckResponse(link, DefaultTimeout);
		public static ValidateCode CheckResponse(Uri link, TimeSpan timeout)
		{
			WebResponse response;
			return GetResponse(out response, link, timeout);
		}

		public static ValidateCode GetResponse(out WebResponse response, Uri link) => GetResponse(out response, link, DefaultTimeout);
		public static ValidateCode GetResponse(out WebResponse response, Uri link, TimeSpan timeout)
		{
			var request = WebRequest.Create(link);
			try
			{
				request.Timeout = (int)timeout.TotalMilliseconds;
				response = request.GetResponse();
				return ValidateCode.Ok;
			}
			catch (WebException webEx)
			{
				response = null;
				HttpWebResponse errorResponse;
				if (webEx.Status == WebExceptionStatus.Timeout)
				{
					Log.Write(Log.Level.Warning, "TH Request timed out");
					return ValidateCode.Timeout;
				}
				else if ((errorResponse = webEx.Response as HttpWebResponse) != null)
				{
					Log.Write(Log.Level.Warning, $"TH Web error: [{(int)errorResponse.StatusCode}] {errorResponse.StatusCode}");
					return ValidateCode.Restricted;
				}
				else
				{
					Log.Write(Log.Level.Warning, $"TH Unknown request error: {webEx}");
					return ValidateCode.UnknownError;
				}
			}
		}
	}

	enum ValidateCode
	{
		Ok,
		UnknownError,
		Restricted,
		Timeout,
	}
}
