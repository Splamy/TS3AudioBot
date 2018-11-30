// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Helper
{
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	internal static class Util
	{
		public static bool IsLinux
		{
			get
			{
				int p = (int)Environment.OSVersion.Platform;
				return (p == 4) || (p == 6) || (p == 128);
			}
		}

		public static IEnumerable<Enum> GetFlags(this Enum input) => Enum.GetValues(input.GetType()).Cast<Enum>().Where(input.HasFlag);

		public static void Init<T>(out T fld) where T : new() => fld = new T();

		public static Encoding Encoder { get; } = new UTF8Encoding(false, false);

		public static readonly DateTime UnixTimeStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

		public static uint UnixNow => (uint)(DateTime.UtcNow - UnixTimeStart).TotalSeconds;

		public static Random Random { get; } = new Random();

		public static DateTime Now => DateTime.UtcNow;

		public static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
		public static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

		public static Exception UnhandledDefault<T>(T value) where T : struct { return new MissingEnumCaseException(typeof(T).Name, value.ToString()); }

		public static CommandError TimeOutCommandError { get; } = CustomError("Connection closed");

		public static CommandError NoResultCommandError { get; } = CustomError("Result is empty");

		public static CommandError ParserCommandError { get; } = CustomError("Result could not be parsed");

		public static CommandError CustomError(string message) => new CommandError { Id = Ts3ErrorCode.custom_error, Message = message };
	}

	internal sealed class MissingEnumCaseException : Exception
	{
		public MissingEnumCaseException(string enumTypeName, string valueName) : base($"The the switch does not handle the value \"{valueName}\" from \"{enumTypeName}\".") { }
		public MissingEnumCaseException(string message, Exception inner) : base(message, inner) { }
	}

	internal static class DebugUtil
	{
		public static string DebugToHex(byte[] bytes) => bytes is null ? "<null>" : DebugToHex(bytes.AsSpan());

		public static string DebugToHex(ReadOnlySpan<byte> bytes)
		{
			char[] c = new char[bytes.Length * 3];
			for (int bx = 0, cx = 0; bx < bytes.Length; ++bx, ++cx)
			{
				byte b = (byte)(bytes[bx] >> 4);
				c[cx] = (char)(b > 9 ? b - 10 + 'A' : b + '0');

				b = (byte)(bytes[bx] & 0x0F);
				c[++cx] = (char)(b > 9 ? b - 10 + 'A' : b + '0');
				c[++cx] = ' ';
			}
			return new string(c);
		}

		public static byte[] DebugFromHex(string hex)
			=> hex.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(x => Convert.ToByte(x, 16)).ToArray();
	}
}
