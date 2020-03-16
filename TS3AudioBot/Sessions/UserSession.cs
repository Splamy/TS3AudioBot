// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Threading;
using TS3AudioBot.CommandSystem;
using Response = System.Func<string, string>;

namespace TS3AudioBot.Sessions
{
	public class UserSession
	{
		private Dictionary<string, object> assocMap;
		protected bool lockToken;

		public Response ResponseProcessor { get; private set; }

		public UserSession()
		{
			ResponseProcessor = null;
		}

		public void SetResponseInstance(Response responseProcessor)
		{
			VerifyLock();

			ResponseProcessor = responseProcessor;
		}

		public void ClearResponse()
		{
			VerifyLock();

			ResponseProcessor = null;
		}

		public bool Get<TData>(string key, out TData value)
		{
			VerifyLock();
			value = default;

			if (assocMap is null)
				return false;

			if (!assocMap.TryGetValue(key, out object valueObj))
				return false;

			if (!(valueObj is TData valueT))
				return false;

			value = valueT;
			return true;
		}

		public void Set<TData>(string key, TData data)
		{
			VerifyLock();

			if (assocMap is null)
				assocMap = new Dictionary<string, object>();

			assocMap[key] = data;
		}

		public virtual IDisposable GetLock()
		{
			var sessionToken = new SessionLock(this);
			sessionToken.Take();
			return sessionToken;
		}

		protected void VerifyLock()
		{
			if (!lockToken)
				throw new InvalidOperationException("No access lock is currently active");
		}

		private class SessionLock : IDisposable
		{
			private readonly UserSession session;
			public SessionLock(UserSession session) { this.session = session; }

			public void Take() { Monitor.Enter(session); session.lockToken = true; }
			public void Free() { Monitor.Exit(session); session.lockToken = false; }
			public void Dispose() => Free();
		}
	}

	public static class UserSessionExtensions
	{
		public static void SetResponse(this UserSession session, Response responseProcessor)
		{
			if (session is null)
				throw new CommandException("No session context", CommandExceptionReason.CommandError);
			session.SetResponseInstance(responseProcessor);
		}
	}
}
