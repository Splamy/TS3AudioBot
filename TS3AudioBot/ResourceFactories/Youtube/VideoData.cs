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
		public VideoData(string link, string qualitydesciption, VideoCodec codec, bool audioOnly = false, bool videoOnly = false)
		{
			Link = link;
			Qualitydesciption = qualitydesciption;
			Codec = codec;
			AudioOnly = audioOnly;
			VideoOnly = videoOnly;
		}

		public string Link { get; }
		public string Qualitydesciption { get; }
		public VideoCodec Codec { get; }
		public bool AudioOnly { get; }
		public bool VideoOnly { get; }

		public override string ToString() => $"{Qualitydesciption} @ {Codec} - {Link}";
	}
}
