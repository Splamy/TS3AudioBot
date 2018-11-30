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
	using System;
	using System.Net;

	public abstract class WebComponent
	{
		internal static readonly Uri Dummy = new Uri("http://dummy/");

		/// <summary>Processes a HTTP request.</summary>
		/// <param name="context">The HTTP context for the call.</param>
		/// <returns>True if the request was handled.</returns>
		public abstract bool DispatchCall(HttpListenerContext context);
	}
}
