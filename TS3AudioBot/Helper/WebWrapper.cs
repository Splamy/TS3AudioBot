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
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Localization;

namespace TS3AudioBot.Helper;

public static class WebWrapper
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

	private static readonly HttpClient httpClient = new(new RedirectHandler(new HttpClientHandler()));

	static WebWrapper()
	{
		ServicePointManager.DefaultConnectionLimit = int.MaxValue;
		httpClient.Timeout = DefaultTimeout;
		httpClient.DefaultRequestHeaders.UserAgent.Clear();
		ProductInfoHeaderValue version = ProductInfoHeaderValue.TryParse($"TS3AudioBot/{Environment.SystemData.AssemblyData.Version}", out var v)
				? v
				: new ProductInfoHeaderValue("TS3AudioBot", "1.3.3.7");
		httpClient.DefaultRequestHeaders.UserAgent.Add(version);
	}

	// Start

	public static HttpRequestMessage Request(string? link) => Request(CreateUri(link));
	public static HttpRequestMessage Request(Uri uri) => new(HttpMethod.Get, uri);

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

	// Return

	public static async Task Send(this HttpRequestMessage request, CancellationToken token = default)
	{
		try
		{
			using (request)
			{
				using var response = await httpClient.SendDefaultAsync(request, token);
			}
		}
		catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
		{
			throw ToLoggedError(ex);
		}
	}

	public static async Task<string> AsString(this HttpRequestMessage request, CancellationToken token = default)
	{
		try
		{
			using (request)
			{
				using var response = await httpClient.SendDefaultAsync(request, token);
				return await response.Content.ReadAsStringAsync(token);
			}
		}
		catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
		{
			throw ToLoggedError(ex);
		}
	}

	public static async Task<T> AsJson<T>(this HttpRequestMessage request, CancellationToken token = default)
	{
		try
		{
			using (request)
			{
				using var response = await httpClient.SendDefaultAsync(request, token);
				using var stream = await response.Content.ReadAsStreamAsync(token);
				var obj = await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: token);
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

	public static async Task ToAction(this HttpRequestMessage request, AsyncHttpAction body, CancellationToken token = default)
	{
		try
		{
			using (request)
			{
				using var response = await httpClient.SendDefaultAsync(request, token);
				await body.Invoke(response, token);
			}
		}
		catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
		{
			throw ToLoggedError(ex);
		}
	}

	public static async Task<T> ToAction<T>(this HttpRequestMessage request, AsyncHttpAction<T> body, CancellationToken token = default)
	{
		try
		{
			using (request)
			{
				using var response = await httpClient.SendDefaultAsync(request, token);
				return await body.Invoke(response, token); // TODO add token ?
			}
		}
		catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
		{
			throw ToLoggedError(ex);
		}
	}

	public static Task ToStream(this HttpRequestMessage request, AsyncStreamAction body, CancellationToken cancellationToken)
		=> request.ToAction(async (response, ct) => await body(await response.Content.ReadAsStreamAsync(ct), ct), cancellationToken);

	public static async Task<HttpResponseMessage> UnsafeResponse(this HttpRequestMessage request, CancellationToken token = default)
	{
		try
		{
			using (request)
			{
				var response = await httpClient.SendDefaultAsync(request, token);
				return response;
			}
		}
		catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
		{
			throw ToLoggedError(ex);
		}
	}

	// Util

	public static string? GetSingle(this HttpHeaders headers, string name)
		=> headers.TryGetValues(name, out var hvals) ? hvals.FirstOrDefault() : null;

	private static async Task<HttpResponseMessage> SendDefaultAsync(this HttpClient client, HttpRequestMessage request, CancellationToken token)
	{
		var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
		CheckOkReturnCodeOrThrow(response);
		return response;
	}

	private static AudioBotException ToLoggedError(Exception ex)
	{
		if (ex is OperationCanceledException webEx)
		{
			Log.Debug(webEx, "Request timed out");
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
			Log.Debug("Web error: [{0}] {1}", (int)response.StatusCode, response.StatusCode);
			throw Error
				.LocalStr($"{strings.error_net_error_status_code} [{(int)response.StatusCode}] {response.StatusCode}");
		}
	}
}

public delegate Task AsyncStreamAction(Stream stream, CancellationToken ct);
public delegate Task AsyncHttpAction(HttpResponseMessage stream, CancellationToken ct);
public delegate Task<T> AsyncHttpAction<T>(HttpResponseMessage stream, CancellationToken ct);

// HttpClient does not allow unsafe HTTPS->HTTP redirects.
// But we don't care because audio streaming is not security critical
// This loop implements a simple redirect following on 301/302 with at most 5 redirects.
public class RedirectHandler : DelegatingHandler
{
	private const int MaxRedirects = 5;

	public RedirectHandler(HttpMessageHandler innerHandler)
		: base(innerHandler)
	{ }

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		HttpResponseMessage response;
		for (int i = 0; i < MaxRedirects; i++)
		{
			response = await base.SendAsync(request, cancellationToken);
			if (response.StatusCode == HttpStatusCode.Moved || response.StatusCode == HttpStatusCode.Redirect)
			{
				request.RequestUri = response.Headers.Location;
			}
			else
			{
				return response;
			}
		}

		throw Error.LocalStr(strings.error_media_internal_invalid + " (Max redirects reached)");
	}
}
