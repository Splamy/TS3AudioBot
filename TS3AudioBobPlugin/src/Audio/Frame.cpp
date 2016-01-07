#include "Frame.hpp"

#include <utility>

using namespace audio;

Frame::Frame() :
	internalFrame(nullptr)
{
}

Frame::Frame(int queueId) :
	internalFrame(av_frame_alloc()),
	queueId(queueId)
{
}

Frame::Frame(Frame &&f) :
	internalFrame(nullptr)
{
	*this = std::move(f);
}

Frame& Frame::operator = (Frame &&f)
{
	std::swap(internalFrame, f.internalFrame);
	queueId = f.queueId;
	playTime = f.playTime;
	duration = f.duration;
	return *this;
}

Frame::~Frame()
{
	if (internalFrame)
		av_frame_free(&internalFrame);
}

int Frame::getSampleCount() const
{
	return internalFrame->nb_samples;
}

AVSampleFormat Frame::getAudioFormat() const
{
	return (AVSampleFormat) internalFrame->format;
}

int Frame::getSampleRate() const
{
	return internalFrame->sample_rate;
}

uint8_t** Frame::getData()
{
	return internalFrame->extended_data;
}

AVFrame* Frame::getInternalFrame()
{
	return internalFrame;
}

int Frame::getQueueId() const
{
	return queueId;
}

void Frame::setQueueId(int queueId)
{
	this->queueId = queueId;
}

int Frame::getChannelCount() const
{
	return av_frame_get_channels(internalFrame);
}

uint64_t Frame::getChannelLayout() const
{
	return internalFrame->channel_layout;
}

double Frame::getPlayTime() const
{
	return playTime;
}

void Frame::setPlayTime(double playTime)
{
	this->playTime = playTime;
}

double Frame::getDuration() const
{
	return duration;
}

void Frame::setDuration(double duration)
{
	this->duration = duration;
}
