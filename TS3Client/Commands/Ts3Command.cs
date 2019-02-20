// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Commands
{
	using Helper;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;

	/// <summary>Builds TeamSpeak (query) commands from parameters.</summary>
	public partial class Ts3Command : IEnumerable, IEnumerable<ICommandPart>
	{
		private static readonly Regex CommandMatch = new Regex("[a-z0-9_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ECMAScript);

		protected string raw = null;
		protected bool cached = false;
		public bool ExpectResponse { get; set; }
		public string Command { get; }
		private ICollection<ICommandPart> parameter;

		/// <summary>Creates a new command.</summary>
		/// <param name="command">The command name.</param>
		[DebuggerStepThrough]
		public Ts3Command(string command)
		{
			ExpectResponse = true;
			Command = command;
		}

		/// <summary>Creates a new command.</summary>
		/// <param name="command">The command name.</param>
		/// <param name="parameter">The parameters to be added to this command.
		/// See <see cref="CommandParameter"/>, <see cref="CommandOption"/> or <see cref="CommandMultiParameter"/> for more information.</param>
		[DebuggerStepThrough]
		public Ts3Command(string command, ICollection<ICommandPart> parameter) : this(command)
		{
			this.parameter = parameter;
		}

		[DebuggerStepThrough]
		public virtual Ts3Command Add(ICommandPart addParameter)
		{
			cached = false;
			parameter = parameter ?? new List<ICommandPart>();
			parameter.Add(addParameter);
			return this;
		}

		/// <summary>
		/// Can be set to false when a command does not receive a error-return-code
		/// from teamspeak.
		/// This makes this command effectively a notification, as a response can only
		/// be sent back as a new notification.
		/// </summary>
		/// <param name="expects">Whether or not to wait for the error-return-code</param>
		/// <returns>Returns this command. Useful for fluent command building.</returns>
		[DebuggerStepThrough]
		public Ts3Command ExpectsResponse(bool expects)
		{
			ExpectResponse = expects;
			return this;
		}

		/// <summary>Builds this command to the query-like command.</summary>
		/// <returns>The formatted query-like command.</returns>
		public override string ToString()
		{
			if (!cached)
			{
				raw = BuildToString(Command, GetParameter());
				cached = true;
			}
			return raw;
		}

		/// <summary>Builds the command from its parameters and returns the query-like command.</summary>
		/// <param name="command">The command name.</param>
		/// <param name="parameter">The parameter to be added to this command.</param>
		/// <returns>The formatted query-like command.</returns>
		/// <exception cref="ArgumentException">When a command is null or not valid.</exception>
		/// <exception cref="ArgumentOutOfRangeException">When multiple <see cref="CommandMultiParameter"/> are added but have different array lengths.</exception>
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
					if (multiParamList is null)
						multiParamList = new List<CommandMultiParameter>();
					multiParamList.Add((CommandMultiParameter)param);
					break;
				case CommandPartType.Option:
					if (optionList is null)
						optionList = new List<CommandOption>();
					optionList.Add((CommandOption)param);
					break;
				default:
					throw Util.UnhandledDefault(param.Type);
				}
			}

			if (multiParamList != null)
			{
				// Safety check
				int matrixLength = multiParamList[0].Values.Length;
				foreach (var param in multiParamList)
					if (param.Values.Length != matrixLength)
						throw new ArgumentOutOfRangeException(nameof(parameter), "All multiparam key-value pairs must have the same length");

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

		private IEnumerable<ICommandPart> GetParameter() => parameter ?? Enumerable.Empty<ICommandPart>();

		public IEnumerator GetEnumerator() => GetParameter().GetEnumerator();
		IEnumerator<ICommandPart> IEnumerable<ICommandPart>.GetEnumerator() => GetParameter().GetEnumerator();
	}

	public class Ts3RawCommand : Ts3Command
	{
		public Ts3RawCommand(string raw) : base(null)
		{
			this.raw = raw;
			this.cached = true;
		}

		public override Ts3Command Add(ICommandPart addParameter)
		{
			throw new InvalidOperationException("Raw commands cannot be extented");
		}

		public override string ToString()
		{
			return raw;
		}
	}
}
