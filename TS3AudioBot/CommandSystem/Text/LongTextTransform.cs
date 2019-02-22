// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem.Text
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using TS3Client.Commands;
	using TS3Client.Helper;

	// TODO fix ts3 stupid byte counting...
	public static class LongTextTransform
	{
		private static readonly byte[] SeparatorWeight = new byte[] { (byte)'\n', (byte)',', (byte)' ' };

		public static IEnumerable<string> Transform(string text, LongTextBehaviour behaviour, int limit = int.MaxValue, int maxMessageSize = Ts3Const.MaxSizeTextMessage)
		{
			if (maxMessageSize < 4)
				throw new ArgumentOutOfRangeException(nameof(maxMessageSize), "The minimum split length must be at least 4 bytes to fit all utf8 characters");

			var list = new List<string>();
			var bytes = Encoding.UTF8.GetBytes(text);
			Span<Ind> splitIndices = stackalloc Ind[SeparatorWeight.Length];

			var block = bytes.AsSpan();
			while (block.Length > 0)
			{
				int tokenCnt = 0;
				int i = 0;
				bool filled = false;

				for (; i < block.Length; i++)
				{
					tokenCnt += Ts3String.IsDoubleChar(block[i]) ? 2 : 1;

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
							splitIndices[j] = new Ind { i = i, tok = tokenCnt };
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
						if (!hasSplit && splitIndices[j].i > 0)
						{
							list.Add(block.Slice(0, splitIndices[j].i + 1).NewUtf8String());
							block = block.Slice(splitIndices[j].i + 1);
							hasSplit = true;
						}
					}
					splitIndices.Fill(new Ind());
				}

				if (!hasSplit)
				{
					// UTF-8 adjustment
					while (i > 0 && (block[i] & 0xC0) == 0x80)
						i--;

					list.Add(block.Slice(0, i).NewUtf8String());
					block = block.Slice(i);
				}

				if (--limit == 0)
					break;
			}
			return list;
		}

		private struct Ind
		{
			public int i;
			public int tok;

			public override string ToString() => $"i:{i} tok:{tok}";
		}
	}
}
