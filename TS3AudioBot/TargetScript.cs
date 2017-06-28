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

namespace TS3AudioBot
{
	using CommandSystem;
	using System;

	class TargetScript
	{
		private const string defaultVoiceScript = "!channel on";
		private const string defaultWhisperScript = "!xecute (!unsubscribe temporary) (!subscribe channeltemp (!getuser channel))";

		private MainBot parent;
		private CommandManager CommandManager => parent.CommandManager;

		public TargetScript(MainBot bot)
		{
			parent = bot;
		}

		public void BeforeResourceStarted(object sender, PlayInfoEventArgs e)
		{
			var mode = AudioValues.audioFrameworkData.AudioMode;
			string script;
			if (mode.StartsWith("!", StringComparison.Ordinal))
				script = mode;
			else if (mode.Equals("voice", StringComparison.OrdinalIgnoreCase))
				script = defaultVoiceScript;
			else if (mode.Equals("whisper", StringComparison.OrdinalIgnoreCase))
				script = defaultWhisperScript;
			else
			{
				Log.Write(Log.Level.Error, "Invalid voice mode");
				return;
			}
			CallScript(script, e.Invoker);
		}

		private void CallScript(string script, InvokerData invoker)
		{
			try
			{
				var info = new ExecutionInformation(parent, invoker, null) { SkipRightsChecks = true };
				CommandManager.CommandSystem.Execute(info, script);
			}
			catch (CommandException) { }
		}
	}
}
