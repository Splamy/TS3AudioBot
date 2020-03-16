// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Runtime.CompilerServices;

namespace TSLib.Helper
{
	internal struct SpanSplitter<T> where T : IEquatable<T>
	{
		public bool HasNext => NextIndex >= 0;
		public int NextIndex { get; private set; }
		private T splitchar;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void First(in ReadOnlySpan<T> span, T split)
		{
			splitchar = split;
			NextIndex = span.IndexOf(split);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<T> Next(in ReadOnlySpan<T> current)
		{
			if (!HasNext)
				throw new InvalidOperationException("No next element in span split");
			var ret = current.Slice(NextIndex + 1);
			NextIndex = ret.IndexOf(splitchar);
			return ret;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<T> Trim(in ReadOnlySpan<T> current) => HasNext ? current.Slice(0, NextIndex) : current;
	}
}
