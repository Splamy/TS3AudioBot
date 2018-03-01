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
	using System.Runtime.CompilerServices;

	internal class SpanSplitter
	{
		public bool HasNext => NextIndex >= 0;
		public int NextIndex { get; private set; }
		private char splitchar;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<char> First(string str, char split)
		{
			splitchar = split;
			var span = str.AsReadOnlySpan();
			NextIndex = span.IndexOf(split);
			return span;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void First(ReadOnlySpan<char> span, char split)
		{
			splitchar = split;
			NextIndex = span.IndexOf(split);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<char> Next(ReadOnlySpan<char> current)
		{
			if(!HasNext)
				throw new InvalidOperationException("No next element in span split");
			var ret = current.Slice(NextIndex + 1);
			NextIndex = ret.IndexOf(splitchar);
			return ret;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<char> Trim(ReadOnlySpan<char> current) => HasNext ? current.Slice(0, NextIndex) : current;
	}
}
