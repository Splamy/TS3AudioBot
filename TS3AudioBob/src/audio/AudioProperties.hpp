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

#ifndef AUDIO_AUDIOPROPERTIES_HPP
#define AUDIO_AUDIOPROPERTIES_HPP

extern "C"
{
#include "libavformat/avformat.h"
}

namespace audio
{
/** Store audio properties */
struct AudioProperties
{
	int frequency;
	int channelCount;
	uint64_t channelLayout;
	AVSampleFormat format;
	int frameSize;
	int bytesPerSecond;
};
}

#endif
