// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Globalization;
using TS3AudioBot.CommandSystem.CommandResults;
using TS3AudioBot.ResourceFactories;

namespace TS3AudioBot.History
{
	public class AudioLogEntry : IAudioResourceResult
	{
		/// <summary>A unique id for each <see cref="ResourceFactories.AudioResource"/>, given by the history system.</summary>
		public int Id { get; set; }
		/// <summary>Left for legacy reasons. The dbid of the teamspeak user, who played this song first.</summary>
		[Obsolete]
		public uint? UserInvokeId { get; set; }
		/// <summary>The Uid of the teamspeak user, who played this song first.</summary>
		public string UserUid { get; set; }
		/// <summary>How often the song has been played.</summary>
		public uint PlayCount { get; set; }
		/// <summary>The last time this song has been played.</summary>
		public DateTime Timestamp { get; set; }

		public AudioResource AudioResource { get; set; }

		public AudioLogEntry()
		{
			PlayCount = 0;
		}

		public AudioLogEntry(int id, AudioResource resource) : this()
		{
			Id = id;
			AudioResource = resource;
		}

		public void SetName(string newName)
		{
			AudioResource.ResourceTitle = newName;
		}

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "[{0}] @ {1} by {2}: {3}, ({4})", Id, Timestamp, UserUid, AudioResource.ResourceTitle, AudioResource);
		}
	}
}
