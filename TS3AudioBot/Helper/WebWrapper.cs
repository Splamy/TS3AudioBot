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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using TS3AudioBot.Localization;

namespace TS3AudioBot.Helper
{
	public static class WebWrapper
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
		private const string TimeoutPropertyKey = "RequestTimeout";

		private static readonly HttpClient httpClient = new HttpClient();

		static WebWrapper()
		{
			ServicePointManager.DefaultConnectionLimit = int.MaxValue;
			httpClient.Timeout = DefaultTimeout;
			httpClient.DefaultRequestHeaders.UserAgent.Clear();
			httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TS3AudioBot", Environment.SystemData.AssemblyData.Version));
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
					using var response = await httpClient.SendDefaultAsync(request);
					CheckOkReturnCodeOrThrow(response);
				}
			}
			catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
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
					using var response = await httpClient.SendAsync(request);
					CheckOkReturnCodeOrThrow(response);
					return await response.Content.ReadAsStringAsync();
				}
			}
			catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
			{
				throw ToLoggedError(ex);
			}
		}

		public static async Task<T> AsJson<T>(this HttpRequestMessage request)
		{
			try
			{
				using (request)
				{
					using var response = await httpClient.SendAsync(request);
					CheckOkReturnCodeOrThrow(response);
					using var stream = await response.Content.ReadAsStreamAsync();
					var obj = await JsonSerializer.DeserializeAsync<T>(stream);
					if (obj is null) throw Error.LocalStr(strings.error_net_empty_response);
					return obj;
				}
			}
			catch (JsonException ex)
			{
				Log.Debug(ex, "Failed to parse json.");
				throw Error.LocalStr(strings.error_media_internal_invalid + " (json-request)");
			}
			catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
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
					using var response = await httpClient.SendDefaultAsync(request);
					CheckOkReturnCodeOrThrow(response);
					await body.Invoke(response);
				}
			}
			catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
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
					using var response = await httpClient.SendDefaultAsync(request);
					CheckOkReturnCodeOrThrow(response);
					return await body.Invoke(response);
				}
			}
			catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
			{
				throw ToLoggedError(ex);
			}
		}

		public static Task ToStream(this HttpRequestMessage request, Func<Stream, Task> body)
			=> request.ToAction(async response => await body(await response.Content.ReadAsStreamAsync()));

		public static async Task<HttpResponseMessage> UnsafeResponse(this HttpRequestMessage request)
		{
			try
			{
				using (request)
				{
					var response = await httpClient.SendDefaultAsync(request);
					CheckOkReturnCodeOrThrow(response);
					return response;
				}
			}
			catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
			{
				throw ToLoggedError(ex);
			}
		}

		public static async Task<Stream> UnsafeStream(this HttpRequestMessage request)
			=> await (await request.UnsafeResponse()).Content.ReadAsStreamAsync();

		// Util

		public static string? GetSingle(this HttpHeaders headers, string name)
			=> headers.TryGetValues(name, out var hvals) ? hvals.FirstOrDefault() : null;

		private static Task<HttpResponseMessage> SendDefaultAsync(this HttpClient client, HttpRequestMessage request)
			=> client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

		private static AudioBotException ToLoggedError(Exception ex)
		{
			if (ex is OperationCanceledException webEx)
			{
				Log.Warn(webEx, "Request timed out");
				throw Error.Exception(ex).LocalStr(strings.error_net_timeout);
			}

			Log.Debug(ex, "Unknown request error");
			throw Error.Exception(ex).LocalStr(strings.error_net_unknown);
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
			{
				Log.Warn("Web error: [{0}] {1}", (int)response.StatusCode, response.StatusCode);
				throw Error
					.LocalStr($"{strings.error_net_error_status_code} [{(int)response.StatusCode}] {response.StatusCode}");
			}
		}
	}
}
