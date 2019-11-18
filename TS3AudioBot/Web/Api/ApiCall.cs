// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Net;
using TSLib;

namespace TS3AudioBot.Web.Api
{
	public class ApiCall : InvokerData
	{
		public string Token { get; set; }
		public IPAddress IpAddress { get; set; }
		public Uri RequestUrl { get; set; }
		public string Body { get; set; }

		public static ApiCall CreateAnonymous() => new ApiCall(AnonymousUid);

		public ApiCall(Uid clientUid, IPAddress ipAddress = null, Uri requestUrl = null, string token = null, string body = null) : base(clientUid)
		{
			Token = token;
			IpAddress = ipAddress;
			RequestUrl = requestUrl;
			Body = body;
		}
	}
}
