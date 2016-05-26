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

#ifndef AUDIO_PACKET_TO_FRAME_DECODER_HPP
#define AUDIO_PACKET_TO_FRAME_DECODER_HPP

extern "C"
{
	#include <libavcodec/avcodec.h>
	#include <libavutil/frame.h>
}

#include <condition_variable>
#include <queue>

namespace audio
{
class Player;

/** Get packets as input and decode them into frames. */
class PacketToFrameDecoder
{
private:
	/** The reference to the player. */
	Player *player;
	AVCodecContext *codecContext;

	bool hasPackets = false;
	/** If the last received packet was a flush packet. */
	bool flushed = false;
	/** the packet that is currently decoded into frames. */
	AVPacket currentPacket;
	/** A modified version of currentPacket
	 *  (the data and size attributes are changed).
	 */
	AVPacket tmpPacket;
	/** Initial values that will be used for a reset when a flush packet is received. */
	int64_t initialPlayTime = AV_NOPTS_VALUE;
	AVRational initialPlayTimeBase;
	/* Current values */
	int64_t nextPlayTime;
	AVRational nextPlayTimeBase;
	/** The queue id of the last packet. */
	int lastQueueId;

public:
	PacketToFrameDecoder(Player *player, AVCodecContext *codecContext);
	~PacketToFrameDecoder();

	void setInitalPlayTime(AVRational playTimeBase, int64_t playTime);

	/** Fill a frame with the received packets
	 *  @return The size of the received frame
	 */
	int fillFrame(AVFrame *frame);
	int getLastQueueId() const;
	bool gotFlush() const;
};
}

#endif
