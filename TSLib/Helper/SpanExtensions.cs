// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TSLib.Helper;

public static class SpanExtensions
{
	public static string NewUtf8String(this ReadOnlySpan<byte> span) => Tools.Utf8Encoder.GetString(span);
	public static string NewUtf8String(this Span<byte> span) => Tools.Utf8Encoder.GetString(span);

	public static SpanTuple<T> SplitAt<T>(this Span<T> span, int index)
	{
		return new(span[..index], span[index..]);
	}

	public static ReadOnlySpanTuple<T> SplitAt<T>(this ReadOnlySpan<T> span, int index)
	{
		return new(span[..index], span[index..]);
	}
}

public ref struct SpanTuple<T>
{
	public Span<T> left;
	public Span<T> right;

	public SpanTuple(Span<T> left, Span<T> right)
	{
		this.left = left;
		this.right = right;
	}

	public void Deconstruct(out Span<T> left, out Span<T> right)
	{
		left = this.left;
		right = this.right;
	}
}

public ref struct ReadOnlySpanTuple<T>
{
	public ReadOnlySpan<T> left;
	public ReadOnlySpan<T> right;

	public ReadOnlySpanTuple(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
	{
		this.left = left;
		this.right = right;
	}

	public void Deconstruct(out ReadOnlySpan<T> left, out ReadOnlySpan<T> right)
	{
		left = this.left;
		right = this.right;
	}
}
