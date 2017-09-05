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
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;

	public class CommandMultiParameter : ICommandPart
	{
		public string Key { get; }
		public string[] Values { get; }
		public CommandPartType Type => CommandPartType.MultiParameter;

		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<bool> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<sbyte> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<byte> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<short> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<ushort> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<int> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<uint> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<long> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<ulong> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<float> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<double> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<string> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<TimeSpan> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
		[DebuggerStepThrough] public CommandMultiParameter(string key, IEnumerable<DateTime> value) { Key = key; Values = value.Select(CommandParameter.Serialize).ToArray(); }
	}
}
