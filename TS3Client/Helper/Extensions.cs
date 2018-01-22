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
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Messages;

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

		internal static string NewString(this ReadOnlySpan<char> span) => new string(span.ToArray());
	}
}
