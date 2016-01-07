#include "PacketToFrameDecoder.hpp"

using namespace audio;

PacketToFrameDecoder::PacketToFrameDecoder(std::queue<AVPacket> *packetQueue,
		std::mutex *packetQueueMutex,
		std::condition_variable *readThreadWaiter,
		std::condition_variable *packetQueueWaiter,
		AVCodecContext *codecContext,
		AVPacket *flushPacket,
		bool *finished) :
	packetQueue(packetQueue),
	packetQueueMutex(packetQueueMutex),
	readThreadWaiter(readThreadWaiter),
	packetQueueWaiter(packetQueueWaiter),
	codecContext(codecContext),
	flushPacket(flushPacket),
	finished(finished)
{
	av_init_packet(&currentPacket);
	currentPacket.data = nullptr;
	currentPacket.size = 0;
}

PacketToFrameDecoder::~PacketToFrameDecoder()
{
	av_packet_unref(&currentPacket);
}

int PacketToFrameDecoder::fillFrame(AVFrame *frame)
{
	int gotFrame = 0;
	do
	{
		if (!hasPackets)
		{
			// Get a packet
			do
			{
				if (packetQueue->empty())
				{
					readThreadWaiter->notify_one();
					// Wait until there are new packets
					std::unique_lock<std::mutex> packetQueueLock(*packetQueueMutex);
					packetQueueWaiter->wait(packetQueueLock, [this]{ return !packetQueue->empty() || *finished; });
					if (*finished)
						return -1;
				}

				// The packet queue should contain something now
				tmpPacket = packetQueue->front();
				packetQueue->pop();
				packetQueueWaiter->notify_one();
				lastQueueId = tmpPacket.stream_index;

				// Check if we got a flush packet
				if (tmpPacket.data == flushPacket->data)
				{
					avcodec_flush_buffers(codecContext);
					nextPlayTime = initialPlayTime;
					nextPlayTimeBase = initialPlayTimeBase;
				}
			} while (tmpPacket.data == flushPacket->data);
			// Free the current packet
			av_packet_unref(&currentPacket);
			currentPacket = tmpPacket;
			hasPackets = true;
		}
		// Decode the packet
		int result = avcodec_decode_audio4(codecContext, frame, &gotFrame, &currentPacket);
		if (gotFrame != 0)
		{
			// Try to set the correct presentation timestamp (pts)
			AVRational timeBase{ 1, frame->sample_rate };
			if (frame->pts != AV_NOPTS_VALUE)
				frame->pts = av_rescale_q(frame->pts, codecContext->time_base, timeBase);
			else if (frame->pkt_pts != AV_NOPTS_VALUE)
				frame->pts = av_rescale_q(frame->pkt_pts, av_codec_get_pkt_timebase(codecContext), timeBase);
			else if (nextPlayTime != AV_NOPTS_VALUE)
				frame->pts = av_rescale_q(nextPlayTime, nextPlayTimeBase, timeBase);

			if (frame->pts != AV_NOPTS_VALUE)
			{
				nextPlayTime = frame->pts + frame->nb_samples;
				nextPlayTimeBase = timeBase;
			}
		}

		if (result < 0)
			hasPackets = false;
		else
		{
			if (tmpPacket.data)
			{
				tmpPacket.data += result;
				tmpPacket.size -= result;
				if (tmpPacket.size <= 0)
					hasPackets = false;
			} else if (gotFrame == 0)
				hasPackets = false;
		}
	} while (gotFrame == 0);
	return gotFrame;
}

int PacketToFrameDecoder::getLastQueueId() const
{
	return lastQueueId;
}
