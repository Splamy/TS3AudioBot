#include "PacketToFrameDecoder.hpp"

#include "Player.hpp"

using namespace audio;

PacketToFrameDecoder::PacketToFrameDecoder(Player *player, AVCodecContext *codecContext) :
	player(player),
	codecContext(codecContext)
{
	av_init_packet(&currentPacket);
	currentPacket.data = nullptr;
	currentPacket.size = 0;
}

PacketToFrameDecoder::~PacketToFrameDecoder()
{
	av_packet_unref(&currentPacket);
}

void PacketToFrameDecoder::setInitalPlayTime(AVRational playTimeBase,
	int64_t playTime)
{
	initialPlayTimeBase = playTimeBase;
	initialPlayTime = playTime;
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
				std::unique_lock<std::mutex> packetQueueLock(player->packetQueueMutex);
				if (player->packetQueue.empty())
				{
					player->readThreadWaiter.notify_one();
					// Wait until there are new packets
					player->packetQueueWaiter.wait(packetQueueLock, [this]{ return !player->packetQueue.empty() || player->finished; });
					if (player->finished)
					{
						av_packet_unref(&currentPacket);
						return -1;
					}
				}

				// The packet queue should contain something now
				tmpPacket = player->packetQueue.front();
				player->packetQueue.pop();
				player->packetQueueWaiter.notify_one();
				lastQueueId = tmpPacket.stream_index;

				// Check if we got a flush packet
				if (tmpPacket.data == player->flushPacket.data)
				{
					avcodec_flush_buffers(codecContext);
					nextPlayTime = initialPlayTime;
					nextPlayTimeBase = initialPlayTimeBase;
					flushed = true;
				} else
					flushed = false;
			} while (tmpPacket.data == player->flushPacket.data);
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

bool PacketToFrameDecoder::gotFlush() const
{
	return flushed;
}
