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

namespace TS3AudioBot.Helper
{
	/// <summary>
	/// The R result wrapper.
	/// The functionality is quite similar to the optional-pattern.
	/// It either represents success or an error + message.
	/// </summary>
	public struct R
	{
		public static readonly R OkR = new R();

		// using default false bool so Ok is true on default
		private readonly bool isError;
		public bool Ok => !isError;
		public string Message { get; }

		private R(string message) { isError = true; Message = message; }
		/// <summary>Creates a new failed result with a message</summary>
		/// <param name="message">The message</param>
		public static R Err(string message) => new R(message);

		public static implicit operator bool(R result) => result.Ok;
		public static implicit operator string(R result) => result.Message;

		public static implicit operator R(string message) => new R(message);

		public override string ToString() => Message;
	}

	/// <summary>
	/// The R&lt;T&gt; result wrapper.
	/// The functionality is quite similar to the optional-pattern.
	/// It either represents success + value or an error + message.
	/// The value is guaranteed to be non-null when successful.
	/// </summary>
	public struct R<T>
	{
		private readonly bool isError;
		public bool Ok => !isError;
		public string Message { get; }
		public T Value { get; }

		private R(T value) { isError = false; Message = null; if (value == null) throw new System.Exception("Return of ok must not be null."); Value = value; }
		private R(string message) { isError = true; Message = message; Value = default(T); }

		/// <summary>Creates a new failed result with a message</summary>
		/// <param name="message">The message</param>
		public static R<T> Err(string message) => new R<T>(message);
		/// <summary>Creates a new successful result with a value</summary>
		/// <param name="value">The value</param>
		public static R<T> OkR(T value) => new R<T>(value);

		public static implicit operator bool(R<T> result) => result.Ok;
		public static implicit operator string(R<T> result) => result.Message;

		public static implicit operator R<T>(T result) => new R<T>(result);
		public static implicit operator R<T>(string message) => new R<T>(message);

		public override string ToString() => Message;
	}
}
