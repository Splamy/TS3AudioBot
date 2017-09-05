// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Web
{
	using System.Security.Principal;

	internal class HttpListenerDigestIdentity : IIdentity
	{
		public string AuthenticationType => "Digest";
		public bool IsAuthenticated { get; }

		public string Name { get; }
		public string Nonce { get; }
		public string Hash { get; }
		public string Realm { get; }
		public string Uri { get; }

		public HttpListenerDigestIdentity(string name, string nonce, string hash, string realm, string uri)
		{
			Name = name;
			Nonce = nonce;
			Hash = hash;
			Realm = realm;
			Uri = uri;

			IsAuthenticated = name != null && hash != null && nonce != null && realm != null && uri != null;
		}
	}
}
