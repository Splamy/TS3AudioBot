// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Diagnostics;
using System.Globalization;
using TSLib.Helper;

namespace TSLib.Commands
{
	/// <summary>Simple parameter which will be expanded to "Key=Value" and automatically escaped.</summary>
	public sealed partial class CommandParameter : ICommandPart
	{
		public string Key { get; }
		public string Value { get; }
		public CommandPartType Type => CommandPartType.SingleParameter;

		[DebuggerStepThrough] public static string Serialize(bool value) => value ? "1" : "0";
		[DebuggerStepThrough] public static string Serialize(sbyte value) => value.ToString(CultureInfo.InvariantCulture);
		[DebuggerStepThrough] public static string Serialize(byte value) => value.ToString(CultureInfo.InvariantCulture);
		[DebuggerStepThrough] public static string Serialize(short value) => value.ToString(CultureInfo.InvariantCulture);
		[DebuggerStepThrough] public static string Serialize(ushort value) => value.ToString(CultureInfo.InvariantCulture);
		[DebuggerStepThrough] public static string Serialize(int value) => value.ToString(CultureInfo.InvariantCulture);
		[DebuggerStepThrough] public static string Serialize(uint value) => value.ToString(CultureInfo.InvariantCulture);
		[DebuggerStepThrough] public static string Serialize(long value) => value.ToString(CultureInfo.InvariantCulture);
		[DebuggerStepThrough] public static string Serialize(ulong value) => value.ToString(CultureInfo.InvariantCulture);
		[DebuggerStepThrough] public static string Serialize(float value) => value.ToString(CultureInfo.InvariantCulture);
		[DebuggerStepThrough] public static string Serialize(double value) => value.ToString(CultureInfo.InvariantCulture);
		[DebuggerStepThrough] public static string Serialize(string value) => TsString.Escape(value);
		[DebuggerStepThrough] public static string Serialize(DateTime value) => Tools.ToUnix(value).ToString(CultureInfo.InvariantCulture);
	}
}
