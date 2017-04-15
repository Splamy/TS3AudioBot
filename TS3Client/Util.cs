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

namespace TS3Client
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System;
	using Messages;

	internal static class Util
	{
		public static IEnumerable<T> Slice<T>(this IList<T> arr, int from) => Slice(arr, from, arr.Count - from);
		public static IEnumerable<T> Slice<T>(this IEnumerable<T> arr, int from, int len) => arr.Skip(from).Take(len);

		public static IEnumerable<Enum> GetFlags(this Enum input) => Enum.GetValues(input.GetType()).Cast<Enum>().Where(enu => input.HasFlag(enu));

		public static void Init<T>(ref T fld) where T : new() => fld = new T();

		public static Encoding Encoder { get; } = new UTF8Encoding(false);

		public static readonly DateTime UnixTimeStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

		public static uint UnixNow => (uint)(DateTime.UtcNow - UnixTimeStart).TotalSeconds;

		public static Random Random { get; } = new Random();

		public static DateTime Now => DateTime.UtcNow;

		public static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
	}

	public static class Extensions
	{
		public static string ErrorFormat(this CommandError error)
		{
			if (error.MissingPermissionId > PermissionId.unknown)
				return $"{error.Id}: the command failed to execute: {error.Message} (missing permission:{error.MissingPermissionId})";
			else
				return $"{error.Id}: the command failed to execute: {error.Message}";
		}
	}

	internal static class DebugUtil
	{
		public static string DebugToHex(byte[] data) => string.Join(" ", data.Select(x => x.ToString("X2")));
	}
}
