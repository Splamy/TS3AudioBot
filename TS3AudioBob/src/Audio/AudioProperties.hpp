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
