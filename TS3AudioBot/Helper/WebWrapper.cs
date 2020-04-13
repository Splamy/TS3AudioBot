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
using System.Threading.Tasks;
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

		public static Task<string> DownloadStringAsync(string? link, params (string name, string value)[] headers)
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				throw Error.LocalStr(strings.error_media_invalid_uri);
			return DownloadStringAsync(uri, headers);
		}
		public static async Task<string> DownloadStringAsync(Uri uri, params (string name, string value)[] headers)
		{
			var request = CreateRequest(uri);

			foreach (var (name, value) in headers)
				request.Headers.Add(name, value);

			try
			{
				using var response = await request.GetResponseAsync();
				using var stream = response.GetResponseStream();
				using var reader = new StreamReader(stream);
				var site = await reader.ReadToEndAsync();
				return site;
			}
			catch (Exception ex)
			{
				throw ToLoggedError(ex);
			}
		}

		public static Task GetResponseAsync(string? link, Func<WebResponse, Task>? body = null, TimeSpan? timeout = null)
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				throw Error.LocalStr(strings.error_media_invalid_uri);
			return GetResponseAsync(uri, body, timeout);
		}
		public static async Task GetResponseAsync(Uri uri, Func<WebResponse, Task>? body = null, TimeSpan? timeout = null)
		{
			var request = CreateRequest(uri, timeout);

			try
			{
				using var response = await request.GetResponseAsync();
				if (body != null)
					await body.Invoke(response);
			}
			catch (Exception ex)
			{
				throw ToLoggedError(ex);
			}
		}

		public static Task<T> GetResponseAsync<T>(string? link, Func<WebResponse, Task<T>> body, TimeSpan? timeout = null)
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				throw Error.LocalStr(strings.error_media_invalid_uri);
			return GetResponseAsync(uri, body, timeout);
		}
		public static async Task<T> GetResponseAsync<T>(Uri uri, Func<WebResponse, Task<T>> body, TimeSpan? timeout = null)
		{
			var request = CreateRequest(uri, timeout);

			try
			{
				using var response = await request.GetResponseAsync();
				return await body.Invoke(response);
			}
			catch (Exception ex)
			{
				throw ToLoggedError(ex);
			}
		}

		public static async Task<Stream> GetResponseUnsafeAsync(string? link, TimeSpan? timeout = null)
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				throw Error.LocalStr(strings.error_media_invalid_uri);

			return await GetResponseUnsafeAsync(uri, timeout);
		}
		public static async Task<Stream> GetResponseUnsafeAsync(Uri uri, TimeSpan? timeout = null)
		{
			var request = CreateRequest(uri, timeout);

			try
			{
				var response = await request.GetResponseAsync();
				return response.GetResponseStream();
			}
			catch (Exception ex)
			{
				throw ToLoggedError(ex);
			}
		}

		private static AudioBotException ToLoggedError(Exception ex)
		{
			if (ex is WebException webEx)
			{
				if (webEx.Status == WebExceptionStatus.Timeout)
				{
					Log.Warn(webEx, "Request timed out");
					throw Error.LocalStr(strings.error_net_timeout).Exception(ex);
				}
				else if (webEx.Response is HttpWebResponse errorResponse)
				{
					Log.Warn(webEx, "Web error: [{0}] {1}", (int)errorResponse.StatusCode, errorResponse.StatusCode);
					throw Error
						.LocalStr($"{strings.error_net_error_status_code} [{(int)errorResponse.StatusCode}] {errorResponse.StatusCode}")
						.Exception(ex);
				}
			}

			Log.Debug(ex, "Unknown request error");
			throw Error.LocalStr(strings.error_net_unknown);
		}

		public static WebRequest CreateRequest(string? link, TimeSpan? timeout = null)
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				throw Error.LocalStr(strings.error_media_invalid_uri);
			return CreateRequest(uri, timeout);
		}
		public static WebRequest CreateRequest(Uri uri, TimeSpan? timeout = null)
		{
			try
			{
				var request = WebRequest.Create(uri);
				request.Timeout = (int)(timeout ?? DefaultTimeout).TotalMilliseconds;
				if (request is HttpWebRequest httpRequest)
				{
					httpRequest.UserAgent = "TS3AudioBot";
					httpRequest.KeepAlive = false;
				}
				return request;
			}
			catch (NotSupportedException ex) { throw Error.LocalStr(strings.error_media_invalid_uri).Exception(ex); }
		}
	}
}
