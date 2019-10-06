// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Helper
{
	using NLog;
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Threading;

	public class LockTrace : IDisposable
	{
		private readonly Logger log;
		private readonly object lockObject;
		private readonly Stopwatch timer = new Stopwatch();
		private string name;

		public LockTrace(object lockObject, Logger log)
		{
			this.lockObject = lockObject;
			this.log = log;
		}

		public IDisposable Get([CallerMemberName] string name = null)
		{
			Monitor.Enter(lockObject);
			this.name = name;
			log.Trace("Lock + {0}", name);
			timer.Restart();
			return this;
		}

		public void Dispose()
		{
			timer.Stop();
			log.Trace("Lock - {0} T:{1}", name, timer.ElapsedMilliseconds);
			name = null;
			Monitor.Exit(lockObject);
		}
	}
}
