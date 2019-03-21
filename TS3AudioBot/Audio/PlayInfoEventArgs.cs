// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Audio
{
	using System;
	using TS3AudioBot.ResourceFactories;

	public sealed class PlayInfoEventArgs : EventArgs
	{
		public InvokerData Invoker { get; }
		public PlayResource PlayResource { get; }
		public AudioResource ResourceData => PlayResource.BaseData;
		public MetaData MetaData { get; }
		public string SourceLink { get; }

		public PlayInfoEventArgs(InvokerData invoker, PlayResource playResource, MetaData meta, string sourceLink)
		{
			Invoker = invoker;
			PlayResource = playResource;
			MetaData = meta;
			SourceLink = sourceLink;
		}
	}
}
