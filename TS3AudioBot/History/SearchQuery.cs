// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TS3AudioBot.History
{
	public class SeachQuery
	{
		public string TitlePart { get; set; }
		public string UserUid { get; set; }
		public DateTime? LastInvokedAfter { get; set; }
		public int MaxResults { get; set; }

		public SeachQuery()
		{
			TitlePart = null;
			UserUid = null;
			LastInvokedAfter = null;
			MaxResults = 10;
		}
	}
}
