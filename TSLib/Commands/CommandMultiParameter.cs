// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TSLib.Commands
{
	/// <summary>Represents an array of data. Will be expanded to a pipe seperated list when sent.
	/// Multiple <see cref="CommandMultiParameter"/> will be merged automatically but will need the same array length.</summary>
	public sealed partial class CommandMultiParameter : ICommandPart
	{
		public string Key { get; }
		public string[] Values { get; }
		public CommandPartType Type => CommandPartType.MultiParameter;
	}
}
