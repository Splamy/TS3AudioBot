// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using TS3AudioBot.CommandSystem;
using Response = System.Func<string, System.Threading.Tasks.Task<string?>>;

namespace TS3AudioBot.Sessions
{
	public class UserSession
	{
		private const string ResponseKey = "response";

		private Dictionary<string, object>? assocMap;
		protected bool lockToken;

		public Response? ResponseProcessor => Get<Response>(ResponseKey, out var val) ? val : null;

		public UserSession() { }

		public void SetResponseInstance(Response responseProcessor) => Set(ResponseKey, responseProcessor);

		public void ClearResponse() => Set<Response?>(ResponseKey, null);

		public bool Get<TData>(string key, [MaybeNullWhen(false)] out TData value) where TData : notnull
		{
			value = default!;

			if (assocMap is null)
				return false;

			if (!assocMap.TryGetValue(key, out var valueObj))
				return false;

			if (!(valueObj is TData valueT))
				return false;

			value = valueT;
			return true;
		}

		public void Set<TData>(string key, TData data)
		{
			if (assocMap is null)
				assocMap = new Dictionary<string, object>();

			if (data is null)
				assocMap.Remove(key);
			else
				assocMap[key] = data;
		}
	}

	public static class UserSessionExtensions
	{
		public static void SetResponse(this UserSession? session, Response responseProcessor)
		{
			if (session is null)
				throw new CommandException("No session context", CommandExceptionReason.CommandError);
			session.SetResponseInstance(responseProcessor);
		}
	}
}
