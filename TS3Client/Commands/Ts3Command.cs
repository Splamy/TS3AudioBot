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
	using System.Diagnostics;
	using System.Text;
	using System.Text.RegularExpressions;

	public class Ts3Command
	{
		private static readonly Regex CommandMatch = new Regex(@"[a-z0-9_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ECMAScript);
		public static List<ICommandPart> NoParameter => new List<ICommandPart>();

		internal bool ExpectResponse { get; set; }
		public string Command { get; }
		private List<ICommandPart> parameter;

		[DebuggerStepThrough]
		public Ts3Command(string command) : this(command, NoParameter) { }
		[DebuggerStepThrough]
		public Ts3Command(string command, List<ICommandPart> parameter)
		{
			ExpectResponse = true;
			this.Command = command;
			this.parameter = parameter;
		}

		public Ts3Command AppendParameter(ICommandPart addParameter)
		{
			parameter.Add(addParameter);
			return this;
		}

		internal Ts3Command ExpectsResponse(bool expects)
		{
			ExpectResponse = expects;
			return this;
		}

		public override string ToString() => BuildToString(Command, parameter);

		public static string BuildToString(string command, IEnumerable<ICommandPart> parameter)
		{
			if (string.IsNullOrWhiteSpace(command))
				throw new ArgumentNullException(nameof(command));
			if (!CommandMatch.IsMatch(command))
				throw new ArgumentException("Invalid command characters", nameof(command));

			var strb = new StringBuilder(Ts3String.Escape(command));
			List<CommandMultiParameter> multiParamList = null;
			List<CommandOption> optionList = null;

			foreach (var param in parameter)
			{
				switch (param.Type)
				{
				case CommandPartType.SingleParameter:
					var singleParam = (CommandParameter)param;
					strb.Append(' ').Append(singleParam.Key).Append('=').Append(singleParam.Value);
					break;
				case CommandPartType.MultiParameter:
					if (multiParamList == null)
						multiParamList = new List<CommandMultiParameter>();
					multiParamList.Add((CommandMultiParameter)param);
					break;
				case CommandPartType.Option:
					if (optionList == null)
						optionList = new List<CommandOption>();
					optionList.Add((CommandOption)param);
					break;
				default:
					throw new InvalidOperationException();
				}
			}

			if (multiParamList != null)
			{
				// Safety check
				int matrixLength = multiParamList[0].Values.Length;
				foreach (var param in multiParamList)
					if (param.Values.Length != matrixLength)
						throw new ArgumentOutOfRangeException("All multiparam key-value pairs must have the same length");
				
				for (int i = 0; i < matrixLength; i++)
				{
					foreach (var param in multiParamList)
					{
						strb.Append(' ').Append(param.Key).Append('=').Append(param.Values[i]);
					}
					strb.Append('|');
				}

				strb.Length--;
			}

			if (optionList != null)
			{
				foreach (var option in optionList)
					strb.Append(option.Value);
			}

			return strb.ToString();
		}
	}
}
