// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.ResourceFactories
{
	public enum MatchCertainty
	{
		/// <summary>"Never" denotes that this factory cannot use this link.</summary>
		Never = 0,
		/// <summary>"OnlyIfLast" Only gets selected if no higher match was found.</summary>
		OnlyIfLast,
		/// <summary>"Maybe" has the lowest priority which also excludes all "OnlyIfLast" factories.</summary>
		Maybe,
		/// <summary>"Probably" will get prioritized over "Maybe" and excludes all "OnlyIfLast" factories.</summary>
		Probably,
		/// <summary>"Always" will reserve a link exclusively for all factories which also said "Always".</summary>
		Always,
	}

	public static class MatchCertaintyExtensions
	{
		public static MatchCertainty ToMatchCertainty(this bool val) => val ? MatchCertainty.Always : MatchCertainty.Never;
	}
}
