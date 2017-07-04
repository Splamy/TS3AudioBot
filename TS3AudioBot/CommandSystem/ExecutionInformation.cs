// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot.CommandSystem
{
	using Sessions;
	using Helper;

	public class ExecutionInformation
	{
		public MainBot Bot { get; }
		private UserSession session = null;
		// TODO session as R ?
		public UserSession Session
		{
			get
			{
				if (session == null)
				{
					var result = Bot.SessionManager.GetSession(InvokerData.ClientUid);
					if (!result.Ok)
						throw new CommandException(result.Message, CommandExceptionReason.InternalError);

					session = result.Value;
				}
				return session;
			}
		}
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
			session = userSession;
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
