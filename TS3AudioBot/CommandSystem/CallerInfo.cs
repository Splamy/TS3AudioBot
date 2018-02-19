// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem
{
	using Sessions;

	public class CallerInfo
	{
		// TODO session as R ?
		public UserSession Session { get; }
		public InvokerData InvokerData { get; }
		public string TextMessage { get; }
		public bool ApiCall => InvokerData.IsApi;
		public bool SkipRightsChecks { get; set; }

		public CallerInfo(InvokerData invoker, string textMessage, UserSession userSession = null)
		{
			TextMessage = textMessage;
			InvokerData = invoker;
			Session = userSession;
		}

		public R Write(string message)
		{
			if (Session != null && InvokerData.Visibiliy.HasValue)
				return Session.Write(message, InvokerData.Visibiliy.Value);
			else
				return "User has no visibility";
		}
	}
}
