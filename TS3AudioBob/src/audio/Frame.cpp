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
	internalFrame->extended_data = nullptr;
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
