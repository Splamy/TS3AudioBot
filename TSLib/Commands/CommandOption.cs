// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Linq;
using System.Text;
using TSLib.Helper;

namespace TSLib.Commands
{
	/// <summary>Command options which will be added with "-name" at the and.</summary>
	public class CommandOption : ICommandPart
	{
		public string Value { get; }
		public CommandPartType Type => CommandPartType.Option;

		public CommandOption(string name) { Value = string.Concat(" -", name); }
		/// <summary>Creates one or many options from the enum.
		/// The enum must be a flag list which will be expanded.
		/// The name of each set enum flag will be used as the option name.</summary>
		/// <param name="values"></param>
		public CommandOption(Enum values)
		{
			var strb = new StringBuilder();
			foreach (var enu in values.GetFlags().Select(enu => enu.ToString()))
				strb.Append(" -").Append(enu);
			Value = strb.ToString();
		}
	}
}
