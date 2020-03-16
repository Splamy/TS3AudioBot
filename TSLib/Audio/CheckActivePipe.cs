// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TSLib.Audio
{
	public class CheckActivePipe : IAudioPipe
	{
		public bool Active => OutStream?.Active ?? false;
		public IAudioPassiveConsumer OutStream { get; set; }

		public void Write(Span<byte> data, Meta meta)
		{
			if (OutStream is null || data.IsEmpty || !Active)
				return;

			OutStream?.Write(data, meta);
		}
	}
}
