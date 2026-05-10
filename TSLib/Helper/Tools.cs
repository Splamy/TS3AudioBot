// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSLib.Helper;

public static class Tools
{
	[System.Runtime.Versioning.SupportedOSPlatformGuard("Linux")]
	public static bool IsLinux => OperatingSystem.IsLinux();

	public static IEnumerable<Enum> GetFlags(this Enum input) =>
		Enum.GetValues(input.GetType()).Cast<Enum>().Where(input.HasFlag);

	// Encoding

	public static Encoding Utf8Encoder { get; } = new UTF8Encoding(false, false);

	// Time

	public static readonly DateTime UnixTimeStart = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

	public static uint ToUnix(this DateTime dateTime) => (uint)(dateTime - UnixTimeStart).TotalSeconds;

	public static DateTime FromUnix(uint unixTimestamp) => UnixTimeStart.AddSeconds(unixTimestamp);

	public static uint UnixNow => unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

	public static DateTime Now => DateTime.UtcNow;

	// Random

	public static T PickRandom<T>(IReadOnlyList<T> collection)
	{
		int pick = Random.Shared.Next(0, collection.Count);
		return collection[pick];
	}

	// Math

	public static TimeSpan Min(this TimeSpan a, TimeSpan b) => a < b ? a : b;
	public static TimeSpan Max(this TimeSpan a, TimeSpan b) => a > b ? a : b;

	public static int MathMod(int x, int mod) => (x % mod + mod) % mod;

	// Generic

	public static void SetLogId(Id id) => SetLogId(id.ToString());
	public static void SetLogId(string id) => ScopeContext.PushProperty("BotId", id);

	public static Exception UnhandledDefault<T>(T value) where T : struct
	{
		return new MissingEnumCaseException(typeof(T).Name, value.ToString() ?? string.Empty);
	}
}
