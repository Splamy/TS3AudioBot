// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Text;

namespace TSLib.Helper
{
	public static class SpanExtensions
	{
		public static string NewUtf8String(this ReadOnlySpan<byte> span)
		{
#if NETCOREAPP2_2 || NETCOREAPP3_0
			return Encoding.UTF8.GetString(span);
#else
			return Encoding.UTF8.GetString(span.ToArray());
#endif
		}

		public static string NewUtf8String(this Span<byte> span) => ((ReadOnlySpan<byte>)span).NewUtf8String();

		public static ReadOnlySpan<byte> Trim(this ReadOnlySpan<byte> span, byte elem) => span.TrimStart(elem).TrimEnd(elem);

		public static ReadOnlySpan<byte> TrimStart(this ReadOnlySpan<byte> span, byte elem)
		{
			int start = 0;
			for (; start < span.Length; start++)
			{
				if (span[start] != elem)
					break;
			}
			return span.Slice(start);
		}

		public static ReadOnlySpan<byte> TrimEnd(this ReadOnlySpan<byte> span, byte elem)
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
