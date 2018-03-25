// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

// ReSharper disable CheckNamespace
#pragma warning disable IDE0016

/// <summary>
/// Provides a safe alternative to Exceptions for error and result wrapping.
/// This type represents either success or an error + message.
/// </summary>
public struct R
{
	public static readonly R OkR = new R();

	public bool Ok => Error == null;
	public string Error { get; }

	private R(string error) { Error = error ?? throw new ArgumentNullException(nameof(error), "Error must not be null."); }
	/// <summary>Creates a new failed result with a message</summary>
	/// <param name="error">The message</param>
	public static R Err(string error) => new R(error);

	public static implicit operator bool(R result) => result.Ok;
	public static implicit operator string(R result) => result.Error;

	public static implicit operator R(string error) => new R(error);

	public override string ToString() => Error;
}

/// <summary>
/// Provides a safe alternative to Exceptions for error and result wrapping.
/// This type represents either success + value or an error + message.
/// The value is guaranteed to be non-null when successful.
/// </summary>
/// <typeparam name="TSuccess">The type of the success value.</typeparam>
public struct R<TSuccess>
{
	private readonly bool isError;
	public bool Ok => !isError;
	public string Error { get; }
	public TSuccess Value { get; }

	private R(TSuccess value) { isError = false; Error = null; if (value == null) throw new ArgumentNullException(nameof(value), "Return of ok must not be null."); Value = value; }
	private R(string error) { isError = true; Error = error ?? throw new ArgumentNullException(nameof(error), "Error must not be null."); Value = default(TSuccess); }
	//internal R(bool isError, TSuccess value)

	/// <summary>Creates a new failed result with a message</summary>
	/// <param name="error">The message</param>
	public static R<TSuccess> Err(string error) => new R<TSuccess>(error);
	/// <summary>Creates a new successful result with a value</summary>
	/// <param name="value">The value</param>
	public static R<TSuccess> OkR(TSuccess value) => new R<TSuccess>(value);

	public static implicit operator bool(R<TSuccess> result) => result.Ok;
	public static implicit operator string(R<TSuccess> result) => result.Error;

	public static implicit operator R<TSuccess>(TSuccess result) => new R<TSuccess>(result);
	public static implicit operator R<TSuccess>(string error) => new R<TSuccess>(error);

	public TSuccess OkOr(TSuccess alt) => Ok ? Value : alt;
	public TSuccess Unwrap() => Ok ? Value : throw new InvalidOperationException("Called upwrap on error");

	public override string ToString() => Error;
}

/// <summary>
/// Provides a safe alternative to Exceptions for error and result wrapping.
/// This type represents either success + value or an error + error-object.
/// The value is guaranteed to be non-null when successful.
/// </summary>
/// <typeparam name="TSuccess">The type of the success value.</typeparam>
/// <typeparam name="TError">The error type.</typeparam>
public struct R<TSuccess, TError>
{
	private readonly bool isError;
	public bool Ok => !isError;
	public TError Error { get; }
	public TSuccess Value { get; }

	private R(TSuccess value) { isError = false; Error = default(TError); if (value == null) throw new ArgumentNullException(nameof(value), "Return of ok must not be null."); Value = value; }
	private R(TError error) { isError = true; Value = default(TSuccess); if (error == null) throw new ArgumentNullException(nameof(error), "Error must not be null."); Error = error; }
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

	// Downwrapping
	public E<TError> OnlyError() => new E<TError>(isError, Error);
	public static implicit operator E<TError>(R<TSuccess, TError> result) => result.OnlyError();
}

/// <summary>
/// Provides a safe alternative to Exceptions for error and result wrapping.
/// This type represents either success or an error + error object.
/// </summary>
/// <typeparam name="TError">The type of the error value.</typeparam>
public struct E<TError>
{
	/// <summary>Represents a successful state.</summary>
	public static E<TError> OkR { get; } = new E<TError>();

	private readonly bool isError;
	public bool Ok => !isError;
	public TError Error { get; }

	private E(TError error) { isError = true; if (error == null) throw new ArgumentNullException(nameof(error), "Error must not be null."); Error = error; }
	internal E(bool isError, TError error) { this.isError = isError; Error = error; } // No null check here, we already check cosistently.

	/// <summary>Creates a new failed result with a message</summary>
	/// <param name="error">The message</param>
	public static E<TError> Err(TError error) => new E<TError>(error);

	public static implicit operator bool(E<TError> result) => result.Ok;
	public static implicit operator TError(E<TError> result) => result.Error;

	public static implicit operator E<TError>(TError result) => new E<TError>(result);

	// Upwrapping
	public R<TSuccess, TError> WithValue<TSuccess>(TSuccess value)
	{
		if (!isError && value == null) throw new ArgumentNullException(nameof(value), "Value must not be null.");
		return new R<TSuccess, TError>(isError, value, Error);
	}
}

#pragma warning restore IDE0016
