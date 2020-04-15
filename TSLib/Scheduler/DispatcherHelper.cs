// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using TSLib.Helper;

namespace TSLib.Scheduler
{
	internal static class DispatcherHelper
	{
		public const string DispatcherTitle = "TS Dispatcher";

		internal static string CreateLogThreadName(string threadName, Id id) => threadName + (id == Id.Null ? "" : $"[{id}]");

		internal static string CreateDispatcherTitle(Id id) => CreateLogThreadName(DispatcherTitle, id);
	}
}
