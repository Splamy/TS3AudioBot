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

		public static R<string, LocalStr> DownloadString(string? link, params (string name, string value)[] headers)
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				return new LocalStr(strings.error_media_invalid_uri);
			return DownloadString(uri, headers);
		}
		public static R<string, LocalStr> DownloadString(Uri uri, params (string name, string value)[] headers)
		{
			if (!CreateRequest(uri).Get(out var request, out var error))
				return error;

			foreach (var (name, value) in headers)
				request.Headers.Add(name, value);

			try
			{
				using var response = request.GetResponse();
				using var stream = response.GetResponseStream();
				using var reader = new StreamReader(stream);
				var site = reader.ReadToEnd();
				return site;
			}
			catch (Exception ex)
			{
				return ToLoggedError(ex);
			}
		}

		public static Task<R<string, LocalStr>> DownloadStringAsync(string? link, params (string name, string value)[] headers)
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				return Task.FromResult(R<string, LocalStr>.Err(new LocalStr(strings.error_media_invalid_uri)));
			return DownloadStringAsync(uri, headers);
		}
		public static async Task<R<string, LocalStr>> DownloadStringAsync(Uri uri, params (string name, string value)[] headers)
		{
			if (!CreateRequest(uri).Get(out var request, out var error))
				return error;

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
				return ToLoggedError(ex);
			}
		}

		public static E<LocalStr> GetResponse(string? link, Action<WebResponse>? body = null, TimeSpan? timeout = null)
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				return new LocalStr(strings.error_media_invalid_uri);
			return GetResponse(uri, body, timeout);
		}
		public static E<LocalStr> GetResponse(Uri uri, Action<WebResponse>? body = null, TimeSpan? timeout = null)
		{
			if (!CreateRequest(uri, timeout).Get(out var request, out var error))
				return error;

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

		public static Task<E<LocalStr>> GetResponseAsync(string? link, Func<WebResponse, Task>? body = null, TimeSpan? timeout = null)
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				return Task.FromResult(E<LocalStr>.Err(new LocalStr(strings.error_media_invalid_uri)));
			return GetResponseAsync(uri, body, timeout);
		}
		public static async Task<E<LocalStr>> GetResponseAsync(Uri uri, Func<WebResponse, Task>? body = null, TimeSpan? timeout = null)
		{
			if (!CreateRequest(uri, timeout).Get(out var request, out var error))
				return error;

			try
			{
				using var response = await request.GetResponseAsync();
				if (body != null)
					await body.Invoke(response);
				return R.Ok;
			}
			catch (Exception ex)
			{
				return ToLoggedError(ex);
			}
		}

		public static R<T, LocalStr> GetResponse<T>(string? link, Func<WebResponse, T> body, TimeSpan? timeout = null) where T : notnull
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				return new LocalStr(strings.error_media_invalid_uri);
			return GetResponse(uri, body, timeout);
		}
		public static R<T, LocalStr> GetResponse<T>(Uri uri, Func<WebResponse, T> body, TimeSpan? timeout = null) where T : notnull
		{
			if (!CreateRequest(uri, timeout).Get(out var request, out var error))
				return error;

			try
			{
				using var response = request.GetResponse();
				var result = body.Invoke(response);
				if (result is null)
					return new LocalStr(strings.error_net_unknown);
				return result;
			}
			catch (Exception ex)
			{
				return ToLoggedError(ex);
			}
		}

		public static Task<R<T, LocalStr>> GetResponseAsync<T>(string? link, Func<WebResponse, Task<T>> body, TimeSpan? timeout = null) where T : notnull
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				return Task.FromResult(R<T, LocalStr>.Err(new LocalStr(strings.error_media_invalid_uri)));
			return GetResponseAsync(uri, body, timeout);
		}
		public static async Task<R<T, LocalStr>> GetResponseAsync<T>(Uri uri, Func<WebResponse, Task<T>> body, TimeSpan? timeout = null) where T : notnull
		{
			if (!CreateRequest(uri, timeout).Get(out var request, out var error))
				return error;

			try
			{
				using var response = await request.GetResponseAsync();
				var result = await body.Invoke(response);
				if (result is null)
					return new LocalStr(strings.error_net_unknown);
				return result;
			}
			catch (Exception ex)
			{
				return ToLoggedError(ex);
			}
		}

		public static R<Stream, LocalStr> GetResponseUnsafe(string? link, TimeSpan? timeout = null)
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				return new LocalStr(strings.error_media_invalid_uri);

			return GetResponseUnsafe(uri, timeout);
		}
		public static R<Stream, LocalStr> GetResponseUnsafe(Uri uri, TimeSpan? timeout = null)
		{
			if (!CreateRequest(uri, timeout).Get(out var request, out var error))
				return error;

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

		public static R<WebRequest, LocalStr> CreateRequest(string? link, TimeSpan? timeout = null)
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				return new LocalStr(strings.error_media_invalid_uri);
			return CreateRequest(uri, timeout);
		}
		public static R<WebRequest, LocalStr> CreateRequest(Uri uri, TimeSpan? timeout = null)
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
			catch (NotSupportedException) { return new LocalStr(strings.error_media_invalid_uri); }
		}
	}
}
