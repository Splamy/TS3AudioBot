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
using System.Net.Http;
using System.Threading.Tasks;
using TS3AudioBot.Localization;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Linq;

namespace TS3AudioBot.Helper
{
	public static class WebWrapper
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
		private const string TimeoutPropertyKey = "RequestTimeout";
		private static readonly JsonSerializer JsonSerializer = JsonSerializer.CreateDefault();

		public static HttpClient HttpClient { get; } = new HttpClient();

		static WebWrapper()
		{
			ServicePointManager.DefaultConnectionLimit = int.MaxValue;
			HttpClient.Timeout = DefaultTimeout;
			HttpClient.DefaultRequestHeaders.UserAgent.Clear();
			HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TS3AudioBot", Environment.SystemData.AssemblyData.Version));
		}

		// Start

		public static HttpRequestMessage Request(string? link) => Request(CreateUri(link));
		public static HttpRequestMessage Request(Uri uri) => new HttpRequestMessage(HttpMethod.Get, uri);

		// Prepare

		public static HttpRequestMessage WithMethod(this HttpRequestMessage request, HttpMethod method)
		{
			request.Method = method;
			return request;
		}

		public static HttpRequestMessage WithHeader(this HttpRequestMessage request, string name, string value)
		{
			request.Headers.Add(name, value);
			return request;
		}

		public static HttpRequestMessage WithTimeout(this HttpRequestMessage request, TimeSpan timeout)
		{
			request.Properties[TimeoutPropertyKey] = timeout;
			return request;
		}

		// Return

		public static async Task Send(this HttpRequestMessage request)
		{
			try
			{
				using (request)
				{
					using var response = await HttpClient.SendAsync(request);
					CheckOkReturnCodeOrThrow(response);
				}
			}
			catch (HttpRequestException ex)
			{
				throw ToLoggedError(ex);
			}
		}

		public static async Task<string> AsString(this HttpRequestMessage request)
		{
			try
			{
				using (request)
				{
					using var response = await HttpClient.SendAsync(request);
					CheckOkReturnCodeOrThrow(response);
					return await response.Content.ReadAsStringAsync();
				}
			}
			catch (HttpRequestException ex)
			{
				throw ToLoggedError(ex);
			}
		}

		public static async Task<T> AsJson<T>(this HttpRequestMessage request) where T : class
		{
			try
			{
				using (request)
				{
					using var response = await HttpClient.SendAsync(request);
					CheckOkReturnCodeOrThrow(response);
					using var stream = await response.Content.ReadAsStreamAsync();
					using var sr = new StreamReader(stream);
					using var reader = new JsonTextReader(sr);
					var obj = JsonSerializer.Deserialize<T>(reader);
					if (obj is null) throw Error.LocalStr(strings.error_net_empty_response);
					return obj;
				}
			}
			catch (JsonException ex)
			{
				Log.Debug(ex, "Failed to parse json.");
				throw Error.LocalStr(strings.error_media_internal_invalid + " (json-request)");
			}
			catch (HttpRequestException ex)
			{
				throw ToLoggedError(ex);
			}
		}

		public static async Task ToAction(this HttpRequestMessage request, Func<HttpResponseMessage, Task> body)
		{
			try
			{
				using (request)
				{
					using var response = await HttpClient.SendAsync(request);
					CheckOkReturnCodeOrThrow(response);
					await body.Invoke(response);
				}
			}
			catch (HttpRequestException ex)
			{
				throw ToLoggedError(ex);
			}
		}

		public static async Task<T> ToAction<T>(this HttpRequestMessage request, Func<HttpResponseMessage, Task<T>> body)
		{
			try
			{
				using (request)
				{
					using var response = await HttpClient.SendAsync(request);
					CheckOkReturnCodeOrThrow(response);
					return await body.Invoke(response);
				}
			}
			catch (HttpRequestException ex)
			{
				throw ToLoggedError(ex);
			}
		}

		public static Task ToStream(this HttpRequestMessage request, Func<Stream, Task> body)
			=> request.ToAction(async response => await body(await response.Content.ReadAsStreamAsync()));

		public static async Task<Stream> UnsafeStream(this HttpRequestMessage request)
		{
			try
			{
				using (request)
				{
					using var response = await HttpClient.SendAsync(request);
					CheckOkReturnCodeOrThrow(response);
					return await response.Content.ReadAsStreamAsync();
				}
			}
			catch (HttpRequestException ex)
			{
				throw ToLoggedError(ex);
			}
		}

		public static string? GetSingle(this HttpHeaders headers, string name)
			=> headers.TryGetValues(name, out var hvals) ? hvals.FirstOrDefault() : null;

		// ======

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

		private static Uri CreateUri(string? link)
		{
			if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
				throw Error.LocalStr(strings.error_media_invalid_uri);
			return uri;
		}

		private static void CheckOkReturnCodeOrThrow(HttpResponseMessage response)
		{
			if (!response.IsSuccessStatusCode)
				throw Error.LocalStr(strings.error_net_unknown); // TODO error code
		}
	}
}
