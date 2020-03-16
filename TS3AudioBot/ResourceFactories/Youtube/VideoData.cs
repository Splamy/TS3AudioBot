// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.ResourceFactories.Youtube
{
	public sealed class VideoData
	{
		public string Link { get; set; }
		public string Qualitydesciption { get; set; }
		public VideoCodec Codec { get; set; }
		public bool AudioOnly { get; set; }
		public bool VideoOnly { get; set; }

		public override string ToString() => $"{Qualitydesciption} @ {Codec} - {Link}";
	}
}
