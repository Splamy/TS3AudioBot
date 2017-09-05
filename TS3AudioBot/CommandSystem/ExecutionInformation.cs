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
	using Helper;

	public class ExecutionInformation
	{
		public MainBot Bot { get; }
		// TODO session as R ?
		public UserSession Session { get; internal set; }
		public InvokerData InvokerData { get; internal set; }
		public string TextMessage { get; }
		public bool ApiCall => InvokerData.IsApi;
		public bool SkipRightsChecks { get; set; }

		private ExecutionInformation() : this(null, null, null) { }
		public ExecutionInformation(MainBot bot, InvokerData invoker, string textMessage, UserSession userSession = null)
		{
			Bot = bot;
			TextMessage = textMessage;
			InvokerData = invoker;
			Session = userSession;
		}

		public bool HasRights(params string[] rights)
		{
			if (SkipRightsChecks)
				return true;
			return Bot.RightsManager.HasAllRights(InvokerData, rights);
		}

		public R Write(string message)
		{
			if (InvokerData.Visibiliy.HasValue)
			{
				Session.Write(message, InvokerData.Visibiliy.Value);
				return R.OkR;
			}
			else
				return "User has no visibility";
		}

		public static readonly ExecutionInformation Debug = new ExecutionInformation { SkipRightsChecks = true };
	}
}
