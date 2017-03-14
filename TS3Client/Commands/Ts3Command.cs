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

namespace TS3Client.Commands
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using System.Text.RegularExpressions;

	public class Ts3Command
	{
		private static readonly Regex CommandMatch = new Regex(@"[a-z0-9_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ECMAScript);
		public static List<CommandParameter> NoParameter => new List<CommandParameter>();
		public static List<CommandOption> NoOptions => new List<CommandOption>();

		public bool ExpectResponse { get; set; }
		public string Command { get; }
		private List<CommandParameter> parameter;
		private List<CommandOption> options;

		public Ts3Command(string command) : this(command, NoParameter) { }
		public Ts3Command(string command, List<CommandParameter> parameter) : this(command, parameter, NoOptions) { }
		public Ts3Command(string command, List<CommandParameter> parameter, List<CommandOption> options)
		{
			ExpectResponse = true;
			this.Command = command;
			this.parameter = parameter;
			this.options = options;
		}

		public void AppendParameter(CommandParameter addParameter) => parameter.Add(addParameter);
		public void AppendOption(CommandOption addOption) => options.Add(addOption);

		public override string ToString() => BuildToString(Command, parameter, options);

		public static string BuildToString(string command, IEnumerable<CommandParameter> parameter, IEnumerable<CommandOption> options)
		{
			if (string.IsNullOrWhiteSpace(command))
				throw new ArgumentNullException(nameof(command));
			if (!CommandMatch.IsMatch(command))
				throw new ArgumentException("Invalid command characters", nameof(command));

			var strb = new StringBuilder(Ts3String.Escape(command));

			foreach (var param in parameter)
				strb.Append(' ').Append(param.QueryString);

			foreach (var option in options)
				strb.Append(option.Value);

			return strb.ToString();
		}
	}
}
