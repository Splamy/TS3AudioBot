// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace TS3Client
{
	using Heijden.DNS;
	using System;
	using System.Net;
	using System.Text.RegularExpressions;

	/// <summary>Provides methods to resovle TSDNS, SRV redirects and nicknames</summary>
	public static class TsDnsResolver
	{
		private const ushort Ts3DefaultPort = 9987;
		private const ushort TsDnsDefaultPort = 41144;
		private const string DnsPrefixTcp = "_tsdns._tcp.";
		private const string DnsPrefixUdp = "_ts3._udp.";
		private const string NicknameLookup = "https://named.myteamspeak.com/lookup?name=";

		/// <summary>Tries to resolve a address string to an ip.</summary>
		/// <param name="address">The address, nickname, etc. to resolve.</param>
		/// <param name="endPoint">The ip address if successfully resolved. Otherwise a dummy.</param>
		/// <returns>Whether the resolve was succesful.</returns>
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

			if (!Uri.TryCreate("http://" + address, UriKind.Absolute, out var uri))
				return false;

			var hasUriPort = string.IsNullOrEmpty(uri.GetComponents(UriComponents.Port, UriFormat.Unescaped));

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


			// Try resolve udp prefix
			// Under this address we'll get ts3 voice server
			var srvEndPoint = ResolveSrv(resolver, DnsPrefixUdp + uri.Host);
			if (srvEndPoint != null)
			{
				if (hasUriPort)
					srvEndPoint.Port = uri.Port;
				endPoint = srvEndPoint;
				return true;
			}

			// split domain to get a list of subdomains, for e.g.:
			// my.cool.subdomain.from.de
			// => from.de
			// => subdomain.from.de
			// => cool.subdomain.from.de
			var domainSplit = uri.Host.Split('.');
			if (domainSplit.Length <= 1)
				return false;

			var domainList = new List<string>();
			for (int i = 1; i < Math.Min(domainSplit.Length, 4); i++)
				domainList.Add(string.Join(".", domainSplit, (domainSplit.Length - (i + 1)), i + 1));

			using (var tcpClient = new TcpClient())
			{
				// Try resolve tcp prefix
				// Under this address we'll get the tsdns server
				foreach (var domain in domainList)
				{
					srvEndPoint = ResolveSrv(resolver, DnsPrefixTcp + domain);
					if (srvEndPoint == null)
						continue;

					endPoint = ResolveTsDns(tcpClient, srvEndPoint, uri.Host);
					if (endPoint != null)
						return true;
				}

				// Try resolve to the tsdns service directly
				foreach (var domain in domainList)
				{
					endPoint = ResolveTsDns(tcpClient, domain, TsDnsDefaultPort, uri.Host);
					if (endPoint != null)
						return true;
				}
			}

			// Try to normally resolve server address
			var hostEntry = Dns.GetHostEntry(uri.Host);
			if (hostEntry.AddressList.Length == 0)
				return false;

			var port = string.IsNullOrEmpty(uri.GetComponents(UriComponents.Port, UriFormat.Unescaped))
				? Ts3DefaultPort
				: uri.Port;

			endPoint = new IPEndPoint(hostEntry.AddressList[0], port);
			return true;
		}

		private static IPEndPoint ResolveSrv(Resolver resolver, string domain)
		{
			var response = resolver.Query(domain, QType.SRV, QClass.IN);

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
			return null;
		}

		private static IPEndPoint ResolveTsDns(TcpClient client, string tsDnsAddress, ushort port, string resolveAddress)
		{
			var hostEntry = Dns.GetHostEntry(tsDnsAddress);
			if (hostEntry.AddressList.Length == 0)
				return null;

			return ResolveTsDns(client, new IPEndPoint(hostEntry.AddressList[0], port), resolveAddress);
		}

		private static IPEndPoint ResolveTsDns(TcpClient client, IPEndPoint tsDnsAddress, string resolveAddress)
		{
			try { client.Connect(tsDnsAddress); }
			catch (SocketException) { return null; }

			var stream = client.GetStream();
			var addBuf = Encoding.ASCII.GetBytes(resolveAddress);
			stream.Write(addBuf, 0, addBuf.Length);
			stream.Flush();

			stream.ReadTimeout = 10_000;
			var readBuffer = new byte[128];
			int readLen = stream.Read(readBuffer, 0, readBuffer.Length);
			var returnString = Encoding.ASCII.GetString(readBuffer, 0, readLen);

			return ParseIpEndPoint(returnString);
		}

		private static readonly Regex IpRegex = new Regex(@"(?<ip>(?:\d{1,3}\.){3}\d{1,3}|\[[0-9a-fA-F:]+\])(?::(?<port>\d{1,5}))?", RegexOptions.ECMAScript | RegexOptions.Compiled);

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
