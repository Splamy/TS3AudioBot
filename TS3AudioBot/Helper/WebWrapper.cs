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
	using Localization;
	using System;
	using System.IO;
	using System.Net;

	public static class WebWrapper
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

		public static E<LocalStr> DownloadString(out string site, Uri link, params (string name, string value)[] optionalHeaders)
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
					if (stream is null)
					{
						site = null;
						return new LocalStr(strings.error_net_empty_response);
					}
					using (var reader = new StreamReader(stream))
					{
						site = reader.ReadToEnd();
						return R.Ok;
					}
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
			WebRequest request;
			try { request = WebRequest.Create(link); }
			catch (NotSupportedException) { return new LocalStr(strings.error_media_invalid_uri); }

			try
			{
				request.Timeout = (int)timeout.TotalMilliseconds;
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

		internal static R<Stream, LocalStr> GetResponseUnsafe(Uri link) => GetResponseUnsafe(link, DefaultTimeout);
		internal static R<Stream, LocalStr> GetResponseUnsafe(Uri link, TimeSpan timeout)
		{
			WebRequest request;
			try { request = WebRequest.Create(link); }
			catch (NotSupportedException) { return new LocalStr(strings.error_media_invalid_uri); }

			try
			{
				request.Timeout = (int)timeout.TotalMilliseconds;
				var stream = request.GetResponse().GetResponseStream();
				if (stream is null)
					return new LocalStr(strings.error_net_empty_response);
				return stream;
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

			Log.Warn(ex, "Unknown request error");
			return new LocalStr(strings.error_net_unknown);
		}
	}
}
