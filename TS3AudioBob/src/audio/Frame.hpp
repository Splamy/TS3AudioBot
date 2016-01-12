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
