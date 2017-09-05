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
	using System.Diagnostics;
	using System.Globalization;

	public class CommandParameter : ICommandPart
	{
		public string Key { get; }
		public string Value { get; }
		public CommandPartType Type => CommandPartType.SingleParameter;

		[DebuggerStepThrough] public static string Serialize(bool value) { return (value ? "1" : "0"); }
		[DebuggerStepThrough] public static string Serialize(sbyte value) { return value.ToString(CultureInfo.InvariantCulture); }
		[DebuggerStepThrough] public static string Serialize(byte value) { return value.ToString(CultureInfo.InvariantCulture); }
		[DebuggerStepThrough] public static string Serialize(short value) { return value.ToString(CultureInfo.InvariantCulture); }
		[DebuggerStepThrough] public static string Serialize(ushort value) { return value.ToString(CultureInfo.InvariantCulture); }
		[DebuggerStepThrough] public static string Serialize(int value) { return value.ToString(CultureInfo.InvariantCulture); }
		[DebuggerStepThrough] public static string Serialize(uint value) { return value.ToString(CultureInfo.InvariantCulture); }
		[DebuggerStepThrough] public static string Serialize(long value) { return value.ToString(CultureInfo.InvariantCulture); }
		[DebuggerStepThrough] public static string Serialize(ulong value) { return value.ToString(CultureInfo.InvariantCulture); }
		[DebuggerStepThrough] public static string Serialize(float value) { return value.ToString(CultureInfo.InvariantCulture); }
		[DebuggerStepThrough] public static string Serialize(double value) { return value.ToString(CultureInfo.InvariantCulture); }
		[DebuggerStepThrough] public static string Serialize(string value) { return Ts3String.Escape(value); }
		[DebuggerStepThrough] public static string Serialize(TimeSpan value) { return value.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture); }
		[DebuggerStepThrough] public static string Serialize(DateTime value) { return (value - Util.UnixTimeStart).TotalSeconds.ToString("F0", CultureInfo.InvariantCulture); }

		[DebuggerStepThrough] public CommandParameter(string key, bool value) { Key = key; Value = Serialize(value); }
		[DebuggerStepThrough] public CommandParameter(string key, sbyte value) { Key = key; Value = Serialize(value); }
		[DebuggerStepThrough] public CommandParameter(string key, byte value) { Key = key; Value = Serialize(value); }
		[DebuggerStepThrough] public CommandParameter(string key, short value) { Key = key; Value = Serialize(value); }
		[DebuggerStepThrough] public CommandParameter(string key, ushort value) { Key = key; Value = Serialize(value); }
		[DebuggerStepThrough] public CommandParameter(string key, int value) { Key = key; Value = Serialize(value); }
		[DebuggerStepThrough] public CommandParameter(string key, uint value) { Key = key; Value = Serialize(value); }
		[DebuggerStepThrough] public CommandParameter(string key, long value) { Key = key; Value = Serialize(value); }
		[DebuggerStepThrough] public CommandParameter(string key, ulong value) { Key = key; Value = Serialize(value); }
		[DebuggerStepThrough] public CommandParameter(string key, float value) { Key = key; Value = Serialize(value); }
		[DebuggerStepThrough] public CommandParameter(string key, double value) { Key = key; Value = Serialize(value); }
		[DebuggerStepThrough] public CommandParameter(string key, string value) { Key = key; Value = Serialize(value); }
		[DebuggerStepThrough] public CommandParameter(string key, TimeSpan value) { Key = key; Value = Serialize(value); }
		[DebuggerStepThrough] public CommandParameter(string key, DateTime value) { Key = key; Value = Serialize(value); }
	}
}