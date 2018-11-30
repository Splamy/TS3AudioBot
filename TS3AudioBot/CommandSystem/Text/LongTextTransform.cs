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
	using System.Collections.Generic;
	using TS3AudioBot.Helper;
	using TS3Client.Commands;

	// TODO fix ts3 stupid byte counting...
	public static class LongTextTransform
	{
		private static readonly char[] SeparatorWeight = new char[] { '\n', ',', ' ' };

		public static IEnumerable<string> Transform(string text, LongTextBehaviour behaviour, int limit = int.MaxValue)
		{
			switch (behaviour)
			{
			case LongTextBehaviour.Drop:
			case LongTextBehaviour.SplitHard:
				int tokenCnt = 0;
				int lastSplit = 0;
				for (int i = 0; i < text.Length; i++)
				{
					var prevTokenCnt = tokenCnt;
					tokenCnt += Ts3String.IsDoubleChar(text[i]) ? 2 : 1;
					if (tokenCnt > Ts3Const.MaxSizeTextMessage) // TODO >= ??
					{
						if (behaviour == LongTextBehaviour.Drop)
							yield break;
						yield return text.Substring(lastSplit, i - lastSplit);
						limit--;
						if (limit == 0)
							yield break;
						lastSplit = i;
						tokenCnt -= prevTokenCnt;
					}
				}
				yield return text.Substring(lastSplit);
				break;

			case LongTextBehaviour.Split:
				tokenCnt = 0;
				lastSplit = 0;
				var splitIndices = new (int i, int tok)[SeparatorWeight.Length];

				for (int i = 0; i < text.Length; i++)
				{
					var prevTokenCnt = tokenCnt;
					tokenCnt += Ts3String.IsDoubleChar(text[i]) ? 2 : 1;

					if (tokenCnt > Ts3Const.MaxSizeTextMessage)
					{
						bool hasSplit = false;
						for (int j = 0; j < SeparatorWeight.Length; j++)
						{
							if (!hasSplit && splitIndices[j].i != 0)
							{
								yield return text.Substring(lastSplit, splitIndices[j].i - lastSplit);
								tokenCnt = 0;
								lastSplit = splitIndices[j].i;
								i = lastSplit - 1;
								hasSplit = true;
							}
							splitIndices[j] = (0, 0);
						}

						if (!hasSplit)
						{
							yield return text.Substring(lastSplit, i - lastSplit);
							tokenCnt -= prevTokenCnt;
							lastSplit = i;
						}

						limit--;
						if (limit == 0)
							yield break;
					}
					else
					{
						for (int j = 0; j < SeparatorWeight.Length; j++)
							if (text[i] == SeparatorWeight[j])
								splitIndices[j] = (i, tokenCnt);
					}
				}
				yield return text.Substring(lastSplit);
				break;

			default:
				throw Util.UnhandledDefault(behaviour);
			}
		}
	}
}
