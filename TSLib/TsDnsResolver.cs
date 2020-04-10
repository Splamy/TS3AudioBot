// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Heijden.Dns.Portable;
using Heijden.DNS;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TSLib.Helper;

namespace TSLib
{
	/// <summary>Provides methods to resolve TSDNS, SRV redirects and nicknames</summary>
	public static class TsDnsResolver
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		public const ushort TsVoiceDefaultPort = 9987;
		public const ushort TsQueryDefaultPort = 10011;
		private const ushort TsDnsDefaultPort = 41144;
		private const string DnsPrefixTcp = "_tsdns._tcp.";
		private const string DnsPrefixUdp = "_ts3._udp.";
		private const string NicknameLookup = "https://named.myteamspeak.com/lookup?name=";
		private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(1);
		public static readonly Resolver Resolver = new Resolver(new[]
		{
			// Google
			new IPEndPoint(new IPAddress(new byte[] { 8,8,8,8 }), 53),
			new IPEndPoint(new IPAddress(new byte[] { 8,8,4,4 }), 53),
			// Cloudflare
			new IPEndPoint(new IPAddress(new byte[] { 1,1,1,1 }), 53),
			new IPEndPoint(new IPAddress(new byte[] { 1,0,0,1 }), 53),
			// OpenDNS
			new IPEndPoint(new IPAddress(new byte[] { 208,67,222,222 }), 53),
			new IPEndPoint(new IPAddress(new byte[] { 208,67,220,220 }), 53),
			// Freenom
			new IPEndPoint(new IPAddress(new byte[] { 80,80,80,80 }), 53),
			new IPEndPoint(new IPAddress(new byte[] { 80,80,81,81 }), 53),
		});

		// TODO maybe change to proper TLRU
		private static readonly ConcurrentDictionary<string, CacheEntry> addressCache
			= new ConcurrentDictionary<string, CacheEntry>();
		private static readonly TimeSpan CacheTimeout = TimeSpan.FromMinutes(10);

		/// <summary>Tries to resolve an address string to an ip.</summary>
		/// <param name="address">The address, nickname, etc. to resolve.</param>
		/// <param name="defaultPort">The default port when no port is specified with the address or the resolved address.</param>
		/// <returns>The ip address if successfully resolved.</returns>
		public static async Task<IPEndPoint?> TryResolve(string address, ushort defaultPort = TsVoiceDefaultPort)
		{
			if (string.IsNullOrEmpty(address)) throw new ArgumentNullException(nameof(address));

			if (addressCache.TryGetValue(address, out var cache) && Tools.Now - cache.Created < CacheTimeout)
				return cache.Ip;

			var endPoint = await TryResolveUncached(address, defaultPort);
			addressCache.TryAdd(address, new CacheEntry(endPoint, Tools.Now));
			return endPoint;
		}

		public static async Task<IPEndPoint?> TryResolveUncached(string address, ushort defaultPort = TsVoiceDefaultPort)
		{
			if (string.IsNullOrEmpty(address)) throw new ArgumentNullException(nameof(address));
			IPEndPoint? endPoint;

			Log.Debug("Trying to look up '{0}'", address);

			// if this address does not look like a domain it might be a nickname
			if (!address.Contains(".") && !address.Contains(":") && address != "localhost")
			{
				Log.Debug("Resolving '{0}' as nickname", address);
				var resolvedNickname = await ResolveNickname(address).ConfigureAwait(false);
				if (resolvedNickname != null)
				{
					Log.Debug("Resolved nickname '{0}' as '{1}'", address, resolvedNickname);
					address = resolvedNickname;
				}
			}

			// host is specified as an IP (+ Port)
			if ((endPoint = ParseIpEndPoint(address, defaultPort)) != null)
			{
				Log.Debug("Address is an ip: '{0}'", endPoint);
				return endPoint;
			}

			if (!Uri.TryCreate("http://" + address, UriKind.Absolute, out var uri))
			{
				Log.Warn("Could not parse address as uri");
				return null;
			}

			// host is a dns name
			var hasUriPort = !string.IsNullOrEmpty(uri.GetComponents(UriComponents.Port, UriFormat.Unescaped));

			// Try resolve udp prefix
			// At this address we'll get ts voice server
			endPoint = await ResolveSrv(Resolver, DnsPrefixUdp + uri.Host).ConfigureAwait(false);
			if (endPoint != null)
			{
				if (hasUriPort)
					endPoint.Port = uri.Port;
				Log.Debug("Address found using _udp prefix '{0}'", endPoint);
				return endPoint;
			}

			// split domain to get a list of subdomains, for e.g.:
			// my.cool.subdomain.from.de
			// => from.de
			// => subdomain.from.de
			// => cool.subdomain.from.de
			var domainSplit = uri.Host.Split('.');
			if (domainSplit.Length <= 1)
				return null;
			var domainList = new List<string>();
			for (int i = 1; i < Math.Min(domainSplit.Length, 4); i++)
				domainList.Add(string.Join(".", domainSplit, domainSplit.Length - (i + 1), i + 1));

			// Try resolve tcp prefix
			// Under this address we'll get the tsdns server
			foreach (var domain in domainList)
			{
				var srvEndPoint = await ResolveSrv(Resolver, DnsPrefixTcp + domain).ConfigureAwait(false);
				if (srvEndPoint is null)
					continue;

				endPoint = await ResolveTsDns(srvEndPoint, uri.Host, defaultPort).ConfigureAwait(false);
				if (endPoint != null)
				{
					if (hasUriPort)
						endPoint.Port = uri.Port;
					Log.Debug("Address found using _tcp prefix '{0}'", endPoint);
					return endPoint;
				}
			}

			// Try resolve to the tsdns service directly
			foreach (var domain in domainList)
			{
				endPoint = await ResolveTsDns(domain, TsDnsDefaultPort, uri.Host, defaultPort).ConfigureAwait(false);
				if (endPoint != null)
					return endPoint;
			}

			// Try to normally resolve server address
			var hostAddress = await ResolveDns(uri.Host).ConfigureAwait(false);
			if (hostAddress is null)
				return null;

			var port = hasUriPort ? uri.Port : defaultPort;
			return new IPEndPoint(hostAddress, port);
		}

		private static async Task<IPEndPoint?> ResolveSrv(Resolver resolver, string domain)
		{
			Log.Trace("Resolving srv record '{0}'", domain);
			Response response;
			try
			{
				response = await resolver.Query(domain, QType.SRV, QClass.IN).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Unexcepted dns resolve error.");
				return null;
			}

			if (response.RecordsSRV.Length > 0)
			{
				var srvRecord = response.RecordsSRV[0];

				var hostAddress = await ResolveDns(srvRecord.TARGET).ConfigureAwait(false);
				if (hostAddress != null)
					return new IPEndPoint(hostAddress, srvRecord.PORT);
			}
			return null;
		}

		private static async Task<IPEndPoint?> ResolveTsDns(string tsDnsAddress, ushort port, string resolveAddress, ushort defaultPort)
		{
			Log.Trace("Looking for the tsdns under '{0}'", tsDnsAddress);
			var hostAddress = await ResolveDns(tsDnsAddress).ConfigureAwait(false);
			if (hostAddress is null)
				return null;

			return await ResolveTsDns(new IPEndPoint(hostAddress, port), resolveAddress, defaultPort).ConfigureAwait(false);
		}

		private static async Task<IPEndPoint?> ResolveTsDns(IPEndPoint tsDnsAddress, string resolveAddress, ushort defaultPort)
		{
			Log.Trace("Looking up tsdns address '{0}'", resolveAddress);
			try
			{
				using var client = new TcpClient();
				var cancelTask = Task.Delay(LookupTimeout);
				var connectTask = client.ConnectAsync(tsDnsAddress.Address, tsDnsAddress.Port).ContinueWith(async t =>
				{
					// Swallow error on connect error
					try { await t; } catch { }
				}, TaskContinuationOptions.OnlyOnFaulted);
				await Task.WhenAny(connectTask, cancelTask);
				if (cancelTask.IsCompleted)
				{
					Log.Debug("Request to '{0}' timed out", tsDnsAddress);
					return null;
				}

				using var stream = client.GetStream();
				var addBuf = Encoding.ASCII.GetBytes(resolveAddress);
				await stream.WriteAsync(addBuf, 0, addBuf.Length);
				await stream.FlushAsync();

				stream.ReadTimeout = (int)LookupTimeout.TotalMilliseconds;
				var readBuffer = new byte[128];
				int readLen = await stream.ReadAsync(readBuffer, 0, readBuffer.Length);
				string returnString = Encoding.ASCII.GetString(readBuffer, 0, readLen);

				return ParseIpEndPoint(returnString, defaultPort);
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Socket error checking '{0}', reason {1}", resolveAddress, ex.Message);
				return null;
			}
		}

		private static async Task<IPAddress?> ResolveDns(string hostOrNameAddress)
		{
			try
			{
				Log.Trace("Lookup dns: '{0}'", hostOrNameAddress);
				IPHostEntry hostEntry = await Dns.GetHostEntryAsync(hostOrNameAddress).ConfigureAwait(false);
				if (hostEntry.AddressList.Length == 0)
					return null;
				return hostEntry.AddressList[0];
			}
			catch (SocketException) { return null; }
		}

		private static readonly Regex IpRegex = new Regex(@"(?<ip>(?:\d{1,3}\.){3}\d{1,3}|\[[0-9a-fA-F:]+\]|localhost)(?::(?<port>\d{1,5}))?", RegexOptions.ECMAScript | RegexOptions.Compiled);

		private static IPEndPoint? ParseIpEndPoint(string address, ushort defaultPort)
		{
			var match = IpRegex.Match(address);
			if (!match.Success)
				return null;

			IPAddress ipAddr;
			if (match.Groups["ip"].Value == "localhost")
				ipAddr = IPAddress.Loopback;
			else if (!IPAddress.TryParse(match.Groups["ip"].Value, out ipAddr))
				return null;

			if (!match.Groups["port"].Success)
				return new IPEndPoint(ipAddr, defaultPort);

			if (!ushort.TryParse(match.Groups["port"].Value, out ushort port))
				return null;

			return new IPEndPoint(ipAddr, port);
		}

		private static async Task<string?> ResolveNickname(string nickname)
		{
			string result;
			try
			{
				var request = WebRequest.Create(NicknameLookup + Uri.EscapeDataString(nickname));
				using var respose = await request.GetResponseAsync().ConfigureAwait(false);
				using var stream = respose.GetResponseStream();
				using var reader = new StreamReader(stream, Tools.Utf8Encoder, false, (int)respose.ContentLength);
				result = await reader.ReadToEndAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Failed to resolve nickname \"{0}\"", nickname);
				return null;
			}
			var splits = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			if (splits.Length == 0)
			{
				Log.Warn("Nickname \"{0}\" has no address entries", nickname);
				return null;
			}

			return splits[0];
		}

		private readonly struct CacheEntry
		{
			public IPEndPoint? Ip { get; }
			public DateTime Created { get; }

			public CacheEntry(IPEndPoint? ip, DateTime created)
			{
				Ip = ip;
				Created = created;
			}
		}
	}
}
