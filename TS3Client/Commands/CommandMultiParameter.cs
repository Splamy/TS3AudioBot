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
