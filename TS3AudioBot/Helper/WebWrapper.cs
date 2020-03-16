// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.IO;
using System.Net;
using TS3AudioBot.Localization;

namespace TS3AudioBot.Helper
{
	public static class WebWrapper
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

		static WebWrapper()
		{
			ServicePointManager.DefaultConnectionLimit = int.MaxValue;
		}

		public static E<LocalStr> DownloadString(out string site, Uri link, params (string name, string value)[] headers)
		{
			WebRequest request;
			try { request = WebRequest.Create(link); }
			catch (NotSupportedException) { site = null; return new LocalStr(strings.error_media_invalid_uri); }

			foreach (var (name, value) in headers)
				request.Headers.Add(name, value);

			try
			{
				request.Timeout = (int)DefaultTimeout.TotalMilliseconds;
				using (var response = request.GetResponse())
				using (var stream = response.GetResponseStream())
				using (var reader = new StreamReader(stream))
				{
					site = reader.ReadToEnd();
					return R.Ok;
				}
			}
			catch (Exception ex)
			{
				site = null;
				return ToLoggedError(ex);
			}
		}

		public static R<string, LocalStr> DownloadString(Uri link, params (string name, string value)[] optionalHeaders)
			=> DownloadString(out var str, link, optionalHeaders).WithValue(str);

		public static E<LocalStr> GetResponse(Uri link) => GetResponse(link, null);
		public static E<LocalStr> GetResponse(Uri link, TimeSpan timeout) => GetResponse(link, null, timeout);
		public static E<LocalStr> GetResponse(Uri link, Action<WebResponse> body) => GetResponse(link, body, DefaultTimeout);
		public static E<LocalStr> GetResponse(Uri link, Action<WebResponse> body, TimeSpan timeout)
		{
			var requestRes = CreateRequest(link, timeout);
			if (!requestRes.Ok) return requestRes.Error;
			var request = requestRes.Value;

			try
			{
				using (var response = request.GetResponse())
				{
					body?.Invoke(response);
				}
				return R.Ok;
			}
			catch (Exception ex)
			{
				return ToLoggedError(ex);
			}
		}
		public static R<T, LocalStr> GetResponse<T>(Uri link, Func<WebResponse, T> body) => GetResponse(link, body, DefaultTimeout);
		public static R<T, LocalStr> GetResponse<T>(Uri link, Func<WebResponse, T> body, TimeSpan timeout)
		{
			var requestRes = CreateRequest(link, timeout);
			if (!requestRes.Ok) return requestRes.Error;
			var request = requestRes.Value;

			try
			{
				using (var response = request.GetResponse())
				{
					var result = body.Invoke(response);
					if ((object)result is null)
						return new LocalStr(strings.error_net_unknown);
					return result;
				}
			}
			catch (Exception ex)
			{
				return ToLoggedError(ex);
			}
		}

		public static E<LocalStr> GetResponseLoc(Uri link, Func<WebResponse, E<LocalStr>> body)
			=> GetResponse(link, body).Flat();
		public static E<LocalStr> GetResponseLoc(Uri link, Func<WebResponse, E<LocalStr>> body, TimeSpan timeout)
			=> GetResponse(link, body, timeout).Flat();

		public static R<Stream, LocalStr> GetResponseUnsafe(string link)
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				return new LocalStr(strings.error_media_invalid_uri);

			return GetResponseUnsafe(uri, DefaultTimeout);
		}

		public static R<Stream, LocalStr> GetResponseUnsafe(Uri link) => GetResponseUnsafe(link, DefaultTimeout);
		public static R<Stream, LocalStr> GetResponseUnsafe(Uri link, TimeSpan timeout)
		{
			var requestRes = CreateRequest(link, timeout);
			if (!requestRes.Ok) return requestRes.Error;
			var request = requestRes.Value;

			try
			{
				var response = request.GetResponse();
				return response.GetResponseStream();
			}
			catch (Exception ex)
			{
				return ToLoggedError(ex);
			}
		}

		private static LocalStr ToLoggedError(Exception ex)
		{
			if (ex is WebException webEx)
			{
				if (webEx.Status == WebExceptionStatus.Timeout)
				{
					Log.Warn(webEx, "Request timed out");
					return new LocalStr(strings.error_net_timeout);
				}
				else if (webEx.Response is HttpWebResponse errorResponse)
				{
					Log.Warn(webEx, "Web error: [{0}] {1}", (int)errorResponse.StatusCode, errorResponse.StatusCode);
					return new LocalStr($"{strings.error_net_error_status_code} [{(int)errorResponse.StatusCode}] {errorResponse.StatusCode}");
				}
			}

			Log.Debug(ex, "Unknown request error");
			return new LocalStr(strings.error_net_unknown);
		}

		public static R<WebRequest, LocalStr> CreateRequest(Uri link) => CreateRequest(link, DefaultTimeout);

		private static R<WebRequest, LocalStr> CreateRequest(Uri link, TimeSpan timeout)
		{
			try
			{
				var request = WebRequest.Create(link);
				request.Timeout = (int)timeout.TotalMilliseconds;
				if (request is HttpWebRequest httpRequest)
				{
					httpRequest.UserAgent = "TS3AudioBot";
					httpRequest.KeepAlive = false;
				}
				return request;
			}
			catch (NotSupportedException) { return new LocalStr(strings.error_media_invalid_uri); }
		}
	}
}
