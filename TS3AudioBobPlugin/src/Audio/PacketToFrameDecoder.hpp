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
/** Get packets as input and decode them into frames. */
class PacketToFrameDecoder
{
private:
	/** The input packet queue. */
	std::queue<AVPacket> *packetQueue;
	/** The mutex to lock the packet queue. */
	std::mutex *packetQueueMutex;
	/** The read thread will be notified if the packet queue gets empty. */
	std::condition_variable *readThreadWaiter;
	/** This decoder will wait if the packet queue is empty. */
	std::condition_variable *packetQueueWaiter;
	AVCodecContext *codecContext;
	/** A special packet used to signal that this decoder should be flushed. */
	AVPacket *flushPacket;
	bool *finished;

	bool hasPackets = false;
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
	PacketToFrameDecoder(std::queue<AVPacket> *packetQueue,
		std::mutex *packetQueueMutex,
		std::condition_variable *readThreadWaiter,
		std::condition_variable *packetQueueWaiter,
		AVCodecContext *codecContext,
		AVPacket *flushPacket,
		bool *finished);
	~PacketToFrameDecoder();

	/** Fill a frame with the received packets
	 *  @return The size of the received frame
	 */
	int fillFrame(AVFrame *frame);
	int getLastQueueId() const;
};
}

#endif
