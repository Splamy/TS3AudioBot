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
using System.Text;
using TSLib.Commands;

namespace TS3AudioBot.History
{
	public class SmartHistoryFormatter : IHistoryFormatter
	{
		// configurable constants
		private const string LineBreak = "\n";
		private const int MinTokenLine = 40;
		private readonly bool fairDistribute = true;
		// resulting constants from configuration
		private static readonly int LineBreakLen = TsString.TokenLength(LineBreak);
		private static readonly int UseableTokenLine = MinTokenLine - LineBreakLen;

		public string ProcessQuery(AudioLogEntry entry, Func<AudioLogEntry, string> format)
		{
			return SubstringToken(format(entry), TsConst.MaxSizeTextMessage);
		}

		public string ProcessQuery(IEnumerable<AudioLogEntry> entries, Func<AudioLogEntry, string> format)
		{
			//! entryLinesRev[0] is the most recent entry
			var entryLinesRev = entries.Select(e =>
			{
				string finStr = format(e);
				return new Line { Value = finStr, TokenLength = TsString.TokenLength(finStr) };
			});

			//! entryLines[n] is the most recent entry
			var entryLines = entryLinesRev.Reverse();

			var queryTokenLen = entryLines.Sum(eL => eL.TokenLength + LineBreakLen);
			StringBuilder strb;

			// If the entire content fits within the ts3 limitation, we can concat and return it.
			if (queryTokenLen <= TsConst.MaxSizeTextMessage)
			{
				if (queryTokenLen == 0) return "Nothing found!";
				strb = new StringBuilder(queryTokenLen, queryTokenLen);
				// we want the most recent entry at the bottom so we reverse the list
				foreach (var eL in entryLines)
					strb.Append(eL.Value).Append(LineBreak);
				return strb.ToString();
			}

			int spareToken = TsConst.MaxSizeTextMessage;
			int listStart = 0;

			// Otherwise we go iteratively through the list to test how many entries we can add with our token
			foreach (var eL in entryLinesRev)
			{
				// if we don't have enough token to fit in the next entry (even in shorted form)
				// then we break and use the last few tokens in the next step to fill up.
				if (spareToken < 0 || (spareToken < MinTokenLine && spareToken < eL.TokenLength))
					break;
				// now the further execution is legal because of either of those cases
				// 1) !(spareToken < MinTokenLine):              entry will be trimmed to MinTokenLine and fits
				// 2) !(spareToken < entryLines[i].TokenLength): entry already fits into spareTokens

				if (eL.TokenLength < MinTokenLine)
				{
					spareToken -= eL.TokenLength;
					listStart++;
				}
				else
				{
					spareToken -= MinTokenLine;
					listStart++;
				}
			}

			//! useList[0] is the most recent entry
			var useList = entryLinesRev.Take(listStart).ToList();

			if (fairDistribute)
			{
				// If the fairDistribute option is active this loop will start out by trying to give each
				// entry an equal fraction of all spareToken.
				for (int i = 0; i < useList.Count; i++)
				{
					if (spareToken <= 0) break;
					int fairBonus = spareToken / (useList.Count - i);
					int available = Math.Min(fairBonus, useList[i].TokenLength);
					useList[i].BonusToken = available;
					spareToken -= available;
				}
			}
			else
			{
				// Now distribute the remaining tokens by first come first serve in reverse order
				// so the more recent an entry is the more token it gets
				foreach (var eL in useList)
				{
					if (spareToken <= 0) break;
					if (eL.TokenLength > UseableTokenLine)
					{
						int available = Math.Min(spareToken, eL.TokenLength - UseableTokenLine);
						eL.BonusToken = available;
						spareToken -= available;
					}
				}
			}

			// now we can just build our result and return
			strb = new StringBuilder(TsConst.MaxSizeTextMessage - spareToken, TsConst.MaxSizeTextMessage);
			for (int i = useList.Count - 1; i >= 0; i--)
			{
				var eL = useList[i];
				if (eL.TokenLength < UseableTokenLine + eL.BonusToken)
					strb.Append(eL.Value).Append(LineBreak);
				else
					strb.Append(SubstringToken(eL.Value, UseableTokenLine + eL.BonusToken)).Append(LineBreak);
			}

			return strb.ToString();
		}

		public static string DefaultAleFormat(AudioLogEntry e)
			=> string.Format("{0} ({2}): {1}", e.Id, e.AudioResource.ResourceTitle, e.UserUid, e.PlayCount, e.Timestamp);

		/// <summary>Trims a string to have the given token count at max.</summary>
		/// <param name="value">The string to substring from the left side.</param>
		/// <param name="token">The max token count.</param>
		/// <returns>The new substring.</returns>
		private static string SubstringToken(string value, int token)
		{
			int tokens = 0;
			for (int i = 0; i < value.Length; i++)
			{
				int addToken = TsString.IsDoubleChar(value[i]) ? 2 : 1;
				if (tokens + addToken > token) return value.Substring(0, i);
				else tokens += addToken;
			}
			return value;
		}

		private class Line
		{
			public string Value { get; set; }
			public int TokenLength { get; set; }
			public int BonusToken { get; set; }

			public override string ToString() => $"[{TokenLength:0000}+{BonusToken:0000}] {Value}";
		}
	}
}
