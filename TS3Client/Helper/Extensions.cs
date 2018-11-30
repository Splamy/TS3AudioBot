// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
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

	/// <summary>Provides useful extension methods and error formatting.</summary>
	public static class Extensions
	{
		public static string ErrorFormat(this CommandError error)
		{
			if (error.MissingPermissionId != Ts3Permission.unknown && error.MissingPermissionId != Ts3Permission.undefined)
				return $"{error.Id}: the command failed to execute: {error.Message} (missing permission:{error.MissingPermissionId})";
			else
				return $"{error.Id}: the command failed to execute: {error.Message}";
		}

		public static R<T, CommandError> WrapSingle<T>(in this R<T[], CommandError> result) where T : class
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

		internal static R<T[], CommandError> UnwrapNotification<T>(in this R<LazyNotification, CommandError> result) where T : class
		{
			if (!result.Ok)
				return result.Error;
			return R<T[], CommandError>.OkR((T[])result.Value.Notifications);
		}

		internal static string NewString(in this ReadOnlySpan<char> span) => span.ToString();

		// TODO add optional improvement when nc2.1 is available
		internal static string NewUtf8String(this ReadOnlySpan<byte> span) => System.Text.Encoding.UTF8.GetString(span.ToArray());

		internal static ReadOnlySpan<byte> Trim(this ReadOnlySpan<byte> span, byte elem) => span.TrimStart(elem).TrimEnd(elem);

		internal static ReadOnlySpan<byte> TrimStart(this ReadOnlySpan<byte> span, byte elem)
		{
			int start = 0;
			for (; start < span.Length; start++)
			{
				if (span[start] != elem)
					break;
			}
			return span.Slice(start);
		}

		internal static ReadOnlySpan<byte> TrimEnd(this ReadOnlySpan<byte> span, byte elem)
		{
			int end = span.Length - 1;
			for (; end >= 0; end--)
			{
				if (span[end] != elem)
					break;
			}
			return span.Slice(0, end + 1);
		}
	}
}
