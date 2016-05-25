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

#ifndef AUDIO_FRAME_HPP
#define AUDIO_FRAME_HPP

extern "C"
{
#include "libavformat/avformat.h"
}

namespace audio
{
/** A wrapper class for AVFrame that stores some additional information. */
class Frame
{
private:
	/** The frame that is wrapped by this object. */
	AVFrame *internalFrame;
	/** The queue id of this frame. */
	int queueId;
	/** The time at which this frame should be played. */
	double playTime;
	/** The duration of this frame. */
	double duration;

public:
	Frame();
	Frame(int queueId);
	Frame(const Frame&) = delete;
	Frame(Frame &&f);
	Frame& operator = (const Frame&) = delete;
	Frame& operator = (Frame &&f);
	~Frame();

	/* Getters and setters */
	int getSampleCount() const;
	AVSampleFormat getAudioFormat() const;
	int getSampleRate() const;
	uint8_t** getData();
	AVFrame* getInternalFrame();

	int getQueueId() const;
	void setQueueId(int queueId);

	int getChannelCount() const;
	uint64_t getChannelLayout() const;

	double getPlayTime() const;
	void setPlayTime(double playTime);

	double getDuration() const;
	void setDuration(double duration);
};
}

#endif
