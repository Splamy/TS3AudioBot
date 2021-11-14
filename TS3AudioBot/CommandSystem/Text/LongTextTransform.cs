// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Linq;
using TSLib.Commands;
using TSLib.Helper;

namespace TS3AudioBot.CommandSystem.Text;

public static class LongTextTransform
{
	private static ReadOnlySpan<byte> SeparatorWeight { get => new byte[] { (byte)'\n', (byte)',', (byte)' ' }; }

	public static IEnumerable<string> Split(string text, LongTextBehaviour behaviour, int maxMessageSize, int limit = int.MaxValue)
	{
		if (maxMessageSize < 4)
			throw new ArgumentOutOfRangeException(nameof(maxMessageSize), "The minimum split length must be at least 4 bytes to fit all utf8 characters");
		if (limit <= 0)
			throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be at least 1");

		// Calculates an upper bound by the following assumptions
		// - Since dotnet uses Unicode strings, any single char can be at max 3 bytes long in UTF8
		// - All TS escaped characters are ASCII, so 1 byte character + 1 byte escape = 2
		// so each char '?' => max( MAX_UTF8 = 3, MAX_ASCII_TS_ESCAPED = 2 ) = 3
		if (text.Length * 3 <= maxMessageSize)
			return new[] { text };

		// If the entire text UTF-8 encoded (*2 since each char could be TS-escaped) fits in one message we can return early.
		var encodedSize = Tools.Utf8Encoder.GetByteCount(text);
		if (encodedSize * 2 <= maxMessageSize)
			return new[] { text };

		var list = new List<string>();
		Span<int> splitIndices = stackalloc int[SeparatorWeight.Length];

		var block = new byte[encodedSize].AsSpan(0, encodedSize);
		encodedSize = Tools.Utf8Encoder.GetBytes(text, block);
		if (block.Length != encodedSize) System.Diagnostics.Debug.Fail("Encoding is weird");

		while (block.Length > 0)
		{
			int tokenCnt = 0;
			int i = 0;
			bool filled = false;

			for (; i < block.Length; i++)
			{
				tokenCnt += TsString.IsDoubleChar(block[i]) ? 2 : 1;

				if (tokenCnt > maxMessageSize)
				{
					if (behaviour == LongTextBehaviour.Drop)
						return Enumerable.Empty<string>();

					filled = true;
					break;
				}

				for (int j = 0; j < SeparatorWeight.Length; j++)
				{
					if (block[i] == SeparatorWeight[j])
					{
						splitIndices[j] = i;
					}
				}
			}

			if (!filled)
			{
				list.Add(block.NewUtf8String());
				break;
			}

			bool hasSplit = false;
			if (behaviour != LongTextBehaviour.SplitHard)
			{
				for (int j = 0; j < SeparatorWeight.Length; j++)
				{
					if (!hasSplit && splitIndices[j] > 0)
					{
						var (left, right) = block.SplitAt(splitIndices[j] + 1);
						list.Add(left.NewUtf8String());
						block = right;
						hasSplit = true;
					}
				}
				splitIndices.Fill(0);
			}

			if (!hasSplit)
			{
				// UTF-8 adjustment
				while (i > 0 && (block[i] & 0xC0) == 0x80)
					i--;

				var (left, right) = block.SplitAt(i);
				list.Add(left.NewUtf8String());
				block = right;
			}

			if (--limit == 0)
				break;
		}
		return list;
	}
}
