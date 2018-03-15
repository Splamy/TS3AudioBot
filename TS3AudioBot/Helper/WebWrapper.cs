// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Helper
{
	using System;
	using System.IO;
	using System.Net;

	public static class WebWrapper
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

		public static bool DownloadString(out string site, Uri link, params (string name, string value)[] optionalHeaders)
		{
			var request = WebRequest.Create(link);
			foreach (var (name, value) in optionalHeaders)
				request.Headers.Add(name, value);
			try
			{
				request.Timeout = (int)DefaultTimeout.TotalMilliseconds;
				using (var response = request.GetResponse())
				{
					var stream = response.GetResponseStream();
					if (stream == null)
					{
						site = null;
						return false;
					}
					using (var reader = new StreamReader(stream))
					{
						site = reader.ReadToEnd();
						return true;
					}
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
		public static ValidateCode GetResponse(Uri link) => GetResponse(link, null);
		public static ValidateCode GetResponse(Uri link, TimeSpan timeout) => GetResponse(link, null, timeout);
		public static ValidateCode GetResponse(Uri link, Action<WebResponse> body) => GetResponse(link, body, DefaultTimeout);
		public static ValidateCode GetResponse(Uri link, Action<WebResponse> body, TimeSpan timeout)
		{
			var request = WebRequest.Create(link);
			try
			{
				request.Timeout = (int)timeout.TotalMilliseconds;
				using (var response = request.GetResponse())
				{
					body?.Invoke(response);
				}
				return ValidateCode.Ok;
			}
			catch (WebException webEx)
			{
				HttpWebResponse errorResponse;
				if (webEx.Status == WebExceptionStatus.Timeout)
				{
					Log.Warn("Request timed out");
					return ValidateCode.Timeout;
				}
				else if ((errorResponse = webEx.Response as HttpWebResponse) != null)
				{
					Log.Warn("Web error: [{0}] {1}", (int)errorResponse.StatusCode, errorResponse.StatusCode);
					return ValidateCode.Restricted;
				}
				else
				{
					Log.Warn("Unknown request error: {0}", webEx);
					return ValidateCode.UnknownError;
				}
			}
		}

		internal static R<Stream> GetResponseUnsafe(Uri link) => GetResponseUnsafe(link, DefaultTimeout);
		internal static R<Stream> GetResponseUnsafe(Uri link, TimeSpan timeout)
		{
			var request = WebRequest.Create(link);
			try
			{
				request.Timeout = (int)timeout.TotalMilliseconds;
				var stream = request.GetResponse().GetResponseStream();
				if (stream == null)
					return "WEB No content";
				return stream;
			}
			catch (WebException webEx)
			{
				if (webEx.Status == WebExceptionStatus.Timeout)
					return "WEB Request timed out";
				if (webEx.Response is HttpWebResponse errorResponse)
					return $"WEB error: [{(int)errorResponse.StatusCode}] {errorResponse.StatusCode}";
				return $"WEB Unknown request error: {webEx}";
			}
		}
	}

	public enum ValidateCode
	{
		Ok,
		UnknownError,
		Restricted,
		Timeout,
	}
}
