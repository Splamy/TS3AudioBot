// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client
{
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	internal static class Util
	{
		public static IEnumerable<T> Slice<T>(this IList<T> arr, int from) => Slice(arr, from, arr.Count - from);
		public static IEnumerable<T> Slice<T>(this IEnumerable<T> arr, int from, int len) => arr.Skip(from).Take(len);

		public static IEnumerable<Enum> GetFlags(this Enum input) => Enum.GetValues(input.GetType()).Cast<Enum>().Where(input.HasFlag);

		public static void Init<T>(out T fld) where T : new() => fld = new T();

		public static Encoding Encoder { get; } = new UTF8Encoding(false);

		public static readonly DateTime UnixTimeStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

		public static uint UnixNow => (uint)(DateTime.UtcNow - UnixTimeStart).TotalSeconds;

		public static Random Random { get; } = new Random();

		public static DateTime Now => DateTime.UtcNow;

		public static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
		public static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

		public static Exception UnhandledDefault<T>(T value) where T : struct { return new MissingEnumCaseException(typeof(T).Name, value.ToString()); }

		public static CommandError TimeOutCommandError { get; } = CustomError("Connection closed");

		public static CommandError NoResultCommandError { get; } = CustomError("Result is empty");

		public static CommandError CustomError(string message) => new CommandError { Id = Ts3ErrorCode.custom_error, Message = message };
	}

	internal sealed class MissingEnumCaseException : Exception
	{
		public MissingEnumCaseException(string enumTypeName, string valueName) : base($"The the switch does not handle the value \"{valueName}\" from \"{enumTypeName}\".") { }
		public MissingEnumCaseException(string message, Exception inner) : base(message, inner) { }
	}

	/// <summary>Provides useful extension methods and error formatting.</summary>
	public static class Extensions
	{
		public static string ErrorFormat(this CommandError error)
		{
			if (error.MissingPermissionId > PermissionId.unknown)
				return $"{error.Id}: the command failed to execute: {error.Message} (missing permission:{error.MissingPermissionId})";
			else
				return $"{error.Id}: the command failed to execute: {error.Message}";
		}

		public static R<T, CommandError> WrapSingle<T>(this R<IEnumerable<T>, CommandError> result) where T : class
		{
			if (result.Ok)
				return WrapSingle(result.Value);
			return R<T, CommandError>.Err(result.Error);
		}

		internal static R<T, CommandError> WrapSingle<T>(this IEnumerable<T> enu) where T : class
		{
			var first = enu.FirstOrDefault();
			if (first != null)
				return R<T, CommandError>.OkR(first);
			return R<T, CommandError>.Err(Util.NoResultCommandError);
		}

		internal static R<IEnumerable<T>, CommandError> UnwrapNotification<T>(this R<LazyNotification, CommandError> result) where T : class
		{
			if (!result.Ok)
				return result.Error;
			return R<IEnumerable<T>, CommandError>.OkR(result.Value.Notifications.Cast<T>());
		}
	}

	internal static class DebugUtil
	{
		public static string DebugToHex(byte[] data) => string.Join(" ", data.Select(x => x.ToString("X2")));
	}
}
