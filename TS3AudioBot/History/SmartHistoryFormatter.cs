// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot.History
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using TS3Client.Commands;

	public class SmartHistoryFormatter : IHistoryFormatter
	{
		private const int TS3MAXLENGTH = 1024;
		// configurable constansts
		private const string LineBreak = "\n";
		private const int MinTokenLine = 40;
		private bool fairDistribute = true;
		// resulting constansts from configuration
		private static readonly int LineBreakLen = Ts3String.TokenLength(LineBreak);
		private static readonly int UseableTokenLine = MinTokenLine - LineBreakLen;

		public string ProcessQuery(AudioLogEntry entry, Func<AudioLogEntry, string> format)
		{
			return SubstringToken(format(entry), TS3MAXLENGTH);
		}

		public string ProcessQuery(IEnumerable<AudioLogEntry> entries, Func<AudioLogEntry, string> format)
		{
			//! entryLinesRev[0] is the most recent entry
			var entryLinesRev = entries.Select(e =>
			{
				string finStr = format(e);
				return new Line { Value = finStr, TokenLength = Ts3String.TokenLength(finStr) };
			});

			//! entryLines[n] is the most recent entry
			var entryLines = entryLinesRev.Reverse();

			var queryTokenLen = entryLines.Sum(eL => eL.TokenLength + LineBreakLen);
			StringBuilder strb;

			// If the entire content fits within the ts3 limitation, we can concat and return it.
			if (queryTokenLen <= TS3MAXLENGTH)
			{
				if (queryTokenLen == 0) return "Nothing found!";
				strb = new StringBuilder(queryTokenLen, queryTokenLen);
				// we want the most recent entry at the bottom so we reverse the list
				foreach (var eL in entryLines)
					strb.Append(eL.Value).Append(LineBreak);
				return strb.ToString();
			}

			int spareToken = TS3MAXLENGTH;
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
				// 2) !(spareToken < entryLines[i].TokenLength): entry alreay fits into spareTokens

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
				// so the more recent a entry is the more token it gets
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
			strb = new StringBuilder(TS3MAXLENGTH - spareToken, TS3MAXLENGTH);
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
			=> string.Format("{0} ({2}): {1}", e.Id, e.AudioResource.ResourceTitle, e.UserInvokeId, e.PlayCount, e.Timestamp);

		/// <summary>Trims a string to have the given token count at max.</summary>
		/// <param name="value">The string to substring from the left side.</param>
		/// <param name="token">The max token count.</param>
		/// <returns>The new substring.</returns>
		private static string SubstringToken(string value, int token)
		{
			int tokens = 0;
			for (int i = 0; i < value.Length; i++)
			{
				int addToken = Ts3String.IsDoubleChar(value[i]) ? 2 : 1;
				if (tokens + addToken > token) return value.Substring(0, i);
				else tokens += addToken;
			}
			return value;
		}

		class Line
		{
			public string Value { get; set; } = null;
			public int TokenLength { get; set; } = 0;
			public int BonusToken { get; set; } = 0;

			public override string ToString() => $"[{TokenLength:0000}+{BonusToken:0000}] {Value}";
		}
	}
}