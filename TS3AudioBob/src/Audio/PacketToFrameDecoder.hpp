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
