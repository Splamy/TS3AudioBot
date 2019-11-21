// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Diagnostics;

namespace System
{
	/// <summary>
	/// Provides a safe alternative to Exceptions for error and result wrapping.
	/// This type represents either success or an error + message.
	/// </summary>
	public static class R
	{
		public static readonly _Ok Ok = new _Ok();
		public static readonly _Error Err = new _Error();
	}

	public readonly struct _Ok { }
	public readonly struct _Error { }

	/// <summary>
	/// Provides a safe alternative to Exceptions for error and result wrapping.
	/// This type represents either success + value or an error + message.
	/// The value is guaranteed to be non-null when successful.
	/// </summary>
	/// <typeparam name="TSuccess">The type of the success value.</typeparam>
	[DebuggerDisplay("{Ok ? (\"Ok : \" + typeof(TSuccess).Name) : \"Err\", nq}")]
	public readonly struct R<TSuccess>
	{
		public static readonly R<TSuccess> ErrR = new R<TSuccess>();

		public bool Ok { get; }
		public TSuccess Value { get; }

		private R(TSuccess value) { Ok = true; if (value == null) throw new ArgumentNullException(nameof(value), "Return of ok must not be null."); Value = value; }

		/// <summary>Creates a new successful result with a value</summary>
		/// <param name="value">The value</param>
		public static R<TSuccess> OkR(TSuccess value) => new R<TSuccess>(value);

		public static implicit operator bool(R<TSuccess> result) => result.Ok;

		public static implicit operator R<TSuccess>(TSuccess result) => new R<TSuccess>(result);

		// Fluent get
		public bool GetOk(out TSuccess value)
		{
			if (Ok)
				value = Value;
			else
				value = default;
			return Ok;
		}

		// Convenience casting
		public static implicit operator R<TSuccess>(_Error _) => ErrR;

		// Unwrapping
		public TSuccess OkOr(TSuccess alt) => Ok ? Value : alt;
		public TSuccess Unwrap() => Ok ? Value : throw new InvalidOperationException("Called upwrap on error");
	}

	/// <summary>
	/// Provides a safe alternative to Exceptions for error and result wrapping.
	/// This type represents either success + value or an error + error-object.
	/// The value is guaranteed to be non-null when successful.
	/// </summary>
	/// <typeparam name="TSuccess">The type of the success value.</typeparam>
	/// <typeparam name="TError">The error type.</typeparam>
	[DebuggerDisplay("{Ok ? (\"Ok : \" + typeof(TSuccess).Name) : (\"Err : \" + typeof(TError).Name), nq}")]
	public readonly struct R<TSuccess, TError>
	{
		private readonly bool isError;
		public bool Ok => !isError;
		public TError Error { get; }
		public TSuccess Value { get; }

		private R(TSuccess value) { isError = false; Error = default; if (value == null) throw new ArgumentNullException(nameof(value), "Return of ok must not be null."); Value = value; }
		private R(TError error) { isError = true; Value = default; if (error == null) throw new ArgumentNullException(nameof(error), "Error must not be null."); Error = error; }
		internal R(bool isError, TSuccess value, TError error) { this.isError = isError; Value = value; Error = error; }

		/// <summary>Creates a new failed result with an error object</summary>
		/// <param name="error">The error</param>
		public static R<TSuccess, TError> Err(TError error) => new R<TSuccess, TError>(error);
		/// <summary>Creates a new successful result with a value</summary>
		/// <param name="value">The value</param>
		public static R<TSuccess, TError> OkR(TSuccess value) => new R<TSuccess, TError>(value);

		public static implicit operator bool(R<TSuccess, TError> result) => result.Ok;
		public static implicit operator TError(R<TSuccess, TError> result) => result.Error;

		public static implicit operator R<TSuccess, TError>(TSuccess result) => new R<TSuccess, TError>(result);
		public static implicit operator R<TSuccess, TError>(TError error) => new R<TSuccess, TError>(error);

		// Fluent get
		public E<TError> GetOk(out TSuccess value)
		{
			if (Ok)
			{
				value = Value;
				return E<TError>.OkR;
			}
			else
			{
				value = default;
				return OnlyError();
			}
		}

		// Unwrapping
		public TSuccess OkOr(TSuccess alt) => Ok ? Value : alt;
		public TSuccess Unwrap() => Ok ? Value : throw new InvalidOperationException("Called upwrap on error");

		// Downwrapping
		public E<TError> OnlyError() => new E<TError>(isError, Error);
		public static implicit operator E<TError>(R<TSuccess, TError> result) => result.OnlyError();
	}

	/// <summary>
	/// Provides a safe alternative to Exceptions for error and result wrapping.
	/// This type represents either success or an error + error object.
	/// </summary>
	/// <typeparam name="TError">The type of the error value.</typeparam>
	[DebuggerDisplay("{Ok ? \"Ok\" : (\"Err : \" + typeof(TError).Name), nq}")]
	public readonly struct E<TError>
	{
		/// <summary>Represents a successful state.</summary>
		public static E<TError> OkR { get; } = new E<TError>();

		private readonly bool isError;
		public bool Ok => !isError;
		public TError Error { get; }

		private E(TError error) { isError = true; if (error == null) throw new ArgumentNullException(nameof(error), "Error must not be null."); Error = error; }
		internal E(bool isError, TError error) { this.isError = isError; Error = error; } // No null check here, we already check cosistently.

		/// <summary>Creates a new failed result with a error object.</summary>
		/// <param name="error">The error object.</param>
		public static E<TError> Err(TError error) => new E<TError>(error);

		public static implicit operator bool(E<TError> result) => result.Ok;
		public static implicit operator TError(E<TError> result) => result.Error;

		public static implicit operator E<TError>(TError result) => new E<TError>(result);

		// Fluent get
		public bool GetError(out TError value)
		{
			if (Ok)
			{
				value = default;
				return false;
			}
			else
			{
				value = Error;
				return true;
			}
		}

		// Convenience casting
		public static implicit operator E<TError>(_Ok _) => OkR;

		// Unwrapping
		public void Unwrap() { if (!Ok) throw new InvalidOperationException("Called upwrap on error"); }

		// Upwrapping
		/// <summary>
		/// Adds a success value to the result.
		/// This will not change the success state of the result.
		/// Therefore the value may only be null when the state is an error.
		/// </summary>
		/// <param name="value">The success value. Can be null when the current state is an error.</param>
		/// <returns>A new combined result.</returns>
		public R<TSuccess, TError> WithValue<TSuccess>(TSuccess value)
		{
			if (!isError && value == null) throw new ArgumentNullException(nameof(value), "Value must not be null.");
			return new R<TSuccess, TError>(isError, value, Error);
		}
	}

	public static class RExtensions
	{
		public static R<TSuccess, TError> Flat<TSuccess, TError>(this R<R<TSuccess, TError>, TError> boxedR)
		{
			if (!boxedR.Ok)
				return boxedR.Error;
			return boxedR.Value;
		}

		public static E<TError> Flat<TError>(this R<E<TError>, TError> boxedR)
		{
			if (!boxedR.Ok)
				return boxedR.Error;
			return boxedR.Value;
		}
	}
}
