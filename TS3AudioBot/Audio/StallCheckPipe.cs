// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using TSLib.Audio;

namespace TS3AudioBot.Audio
{
	public class StallCheckPipe : IAudioPipe
	{
		private const uint StallCountInterval = 10;
		private const uint StallNoErrorCountMax = 5;

		public bool Active => OutStream?.Active ?? false;
		public IAudioPassiveConsumer OutStream { get; set; }

		private bool isStall;
		private uint stallCount;
		private uint stallNoErrorCount;

		public StallCheckPipe()
		{
			isStall = false;
			stallCount = 0;
		}

		public void Write(Span<byte> data, Meta meta)
		{
			if (OutStream is null) return;

			if (isStall)
			{
				// TODO maybe do time-cooldown instead of call-count-cooldown
				if (++stallCount % StallCountInterval == 0)
				{
					stallNoErrorCount++;
					if (stallNoErrorCount > StallNoErrorCountMax)
					{
						stallCount = 0;
						isStall = false;
					}
				}
				else
				{
					return;
				}
			}

			OutStream?.Write(data, meta);
		}

		public void SetStall()
		{
			stallNoErrorCount = 0;
			isStall = true;
		}
	}
}
