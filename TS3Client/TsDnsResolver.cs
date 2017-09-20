// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client
{
	using Heijden.DNS;
	using System;
	using System.Net;
	using System.Text.RegularExpressions;

	public static class TsDnsResolver
	{
		private const ushort Ts3DefaultPort = 9987;
		private const string DnsPrefixTcp = "_tsdns._tcp.";
		private const string DnsPrefixUdp = "_ts3._udp.";
		private const string NicknameLookup = "https://named.myteamspeak.com/lookup?name=";

		public static bool TryResolve(string address, out IPEndPoint endPoint)
		{
			// if this address does not look like a domain it might be a nickname
			if (!address.Contains(".") && !address.Contains(":") && address != "localhost")
			{
				var resolvedNickname = ResolveNickname(address);
				if (resolvedNickname != null)
					address = resolvedNickname;
			}

			// host is specified as an IP (+ Port)
			if ((endPoint = ParseIpEndPoint(address)) != null)
				return true;

			// host is a dns name
			var resolver = new Resolver
			{
				Recursion = true,
				Retries = 3,
				TimeOut = 1000,
				UseCache = true,
				DnsServer = "8.8.8.8",
				TransportType = Heijden.DNS.TransportType.Udp,
			};

			// Try resolve tcp prefix
			// Under this address we'll get the tsdns server
			var srvEndPoint = ResolveSrv(resolver, DnsPrefixTcp + address);
			if (srvEndPoint != null)
			{
				// Do something i guess?
			}

			// Try resolve udp prefix
			// Under this address we'll get ts3 voice server
			srvEndPoint = ResolveSrv(resolver, DnsPrefixUdp + address);
			if (srvEndPoint != null)
			{
				endPoint = srvEndPoint;
				return true;
			}

			// Try to normally resolve server address
			if (Uri.TryCreate("http://" + address, UriKind.Absolute, out var uri))
			{
				var hostEntry = Dns.GetHostEntry(uri.Host);
				if (hostEntry.AddressList.Length == 0)
					return false;

				var port = string.IsNullOrEmpty(uri.GetComponents(UriComponents.Port, UriFormat.Unescaped))
					? Ts3DefaultPort
					: uri.Port;

				endPoint = new IPEndPoint(hostEntry.AddressList[0], port);
				return true;
			}

			return false;
		}

		private static IPEndPoint ResolveSrv(Resolver resolver, string address)
		{
			if (Uri.TryCreate("http://" + address, UriKind.Absolute, out var uri))
			{
				var response = resolver.Query(uri.Host, QType.SRV, QClass.IN);

				if (response.RecordsSRV.Length > 0)
				{
					var srvRecord = response.RecordsSRV[0];

					var hostEntry = Dns.GetHostEntry(srvRecord.TARGET);
					if (hostEntry.AddressList.Length > 0)
					{
						var hostAddress = hostEntry.AddressList[0];
						return new IPEndPoint(hostAddress, srvRecord.PORT);
					}
				}
			}
			return null;
		}

		private static Regex IpRegex = new Regex(@"(?<ip>(?:\d{1,3}\.){3}\d{1,3}|\[[0-9a-fA-F:]+\])(?::(?<port>\d{1,6}))?", RegexOptions.ECMAScript | RegexOptions.Compiled);

		private static IPEndPoint ParseIpEndPoint(string address)
		{
			var match = IpRegex.Match(address);
			if (!match.Success || !IPAddress.TryParse(match.Groups["ip"].Value, out IPAddress ipAddr))
				return null;

			if (!match.Groups["port"].Success)
				return new IPEndPoint(ipAddr, Ts3DefaultPort);

			if (!ushort.TryParse(match.Groups["port"].Value, out ushort port))
				return null;

			return new IPEndPoint(ipAddr, port);
		}

		private static string ResolveNickname(string nickname)
		{
			string result;
			using (var webClient = new WebClient())
			{
				try { result = webClient.DownloadString(NicknameLookup + Uri.EscapeDataString(nickname)); }
				catch (WebException) { return null; }
			}
			var splits = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			if (splits.Length == 0)
				return null;

			return splits[0];
		}
	}
}
