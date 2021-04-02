// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace TS3AudioBot.Web.Api
{
	public class DataStream
	{
		private readonly Func<HttpResponse, Task> writeFunc;

		public DataStream(Func<HttpResponse, Task> writeFunc)
		{
			this.writeFunc = writeFunc;
		}

		public Task WriteOut(HttpResponse response) => writeFunc(response);

		public override string? ToString() => null;
	}
}
