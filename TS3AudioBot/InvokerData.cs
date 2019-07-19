// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot
{
	using System;

	public class InvokerData
	{
		public string ClientUid { get; }
		public bool IsAnonymous => ClientUid == AnonymousUid;

		protected const string AnonymousUid = "Anonymous";
		public static readonly InvokerData Anonymous = new InvokerData(AnonymousUid);

		public InvokerData(string clientUid)
		{
			ClientUid = clientUid ?? throw new ArgumentNullException(nameof(clientUid));
		}
	}
}
