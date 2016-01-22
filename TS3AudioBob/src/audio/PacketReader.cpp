#include "PacketReader.hpp"

#include "PacketToFrameDecoder.hpp"
#include "Player.hpp"

#ifdef __linux__
	#include <pthread.h>
#endif

using namespace audio;

PacketReader::PacketReader(Player *player) :
	player(player)
{
}

PacketReader::~PacketReader()
{
}

void PacketReader::read()
{
	// Set thread name if available
#ifdef __linux__
	pthread_setname_np(pthread_self(), "ReadThread");
#endif

	if (avformat_open_input(&player->formatContext, player->streamAddress.c_str(), nullptr, nullptr) != 0)
	{
		player->setReadError(Player::READ_ERROR_STREAM_OPEN);
		return;
	}

	AVFormatContext *formatContext = player->formatContext;

	av_format_inject_global_side_data(formatContext);
	if (avformat_find_stream_info(formatContext, nullptr) < 0)
	{
		// Free memory
		avformat_close_input(&player->formatContext);
		player->setReadError(Player::READ_ERROR_STREAM_INFO);
		return;
	}

	// Print audio stream information
	av_dump_format(player->formatContext, 0, player->streamAddress.c_str(), 0);

	// Test if this stream is realtime
	const char *formatName = formatContext->iformat->name;
	bool realtime = strcmp(formatName, "rtp") == 0 || strcmp(formatName, "rtsp") == 0 || strcmp(formatName, "sdp");
	if (formatContext->pb)
	{
		const char *filename = formatContext->filename;
		realtime |= strncmp(filename, "rtp:", 4) == 0 || strncmp(filename, "udp:", 4);
	}

	// Find audio stream
	int audioStreamId = av_find_best_stream(formatContext, AVMEDIA_TYPE_AUDIO, -1, -1, nullptr, 0);
	if (audioStreamId < 0)
	{
		avformat_close_input(&player->formatContext);
		player->setReadError(Player::READ_ERROR_NO_AUDIO_STREAM);
		return;
	}

	if (!openStreamComponent(audioStreamId))
		return;

	{
		// Read the stream
		bool lastPaused = false;
		// The mutex and lock to wait with the readThreadWaiter
		std::unique_lock<std::mutex> waitLock(player->readThreadMutex);
		AVPacket packet;
		bool hasEof = false;
		// Ready with initializing
		player->initialized = true;
		while (!player->finished)
		{
			if (player->paused != lastPaused && !realtime)
			{
				lastPaused = player->paused;
				if (player->paused)
				{
					av_read_pause(formatContext);
					player->pausedWaiter.wait(waitLock, [this]{ return !player->paused || player->finished; });
				} else
					av_read_play(formatContext);
			}

			// Stop reading if the queue is full and if it's not a realtime stream
			if (!realtime && (player->packetQueue.size() >= Player::MAX_QUEUE_SIZE || player->paused))
			{
				player->readThreadWaiter.wait_for(waitLock, std::chrono::milliseconds(10));
				continue;
			}

			// Test if the stream is over
			{
				bool finished;
				// Lock only this part because setPositionTime also locks the packet
				// queue
				{
					std::lock_guard<std::mutex> packetQueueLock(player->packetQueueMutex);
					finished = !player->paused && player->packetQueue.empty() && !player->decoder->gotFlush();
				}
				if (finished)
				{
					if (player->loop)
						player->setPositionTime(0);
					else
						break;
				}
			}

			// Read a packet from the stream
			int ret = av_read_frame(formatContext, &packet);
			if (ret < 0)
			{
				if ((ret == AVERROR_EOF || avio_feof(formatContext->pb)) && !hasEof)
				{
					player->packetQueue.push(player->flushPacket);
					player->packetQueueWaiter.notify_one();
					hasEof = true;
				}
				if (formatContext->pb && formatContext->pb->error)
				{
					player->setReadError(Player::READ_ERROR_IO, false);
					return;
				}

				// Wait for more data
				player->readThreadWaiter.wait_for(waitLock, std::chrono::milliseconds(10));
				continue;
			} else
				hasEof = 1;

			// Insert the packet into the queue
			std::lock_guard<std::mutex> packetQueueLock(player->packetQueueMutex);
			player->packetQueue.push(packet);
			player->packetQueueWaiter.notify_one();
		}
	}
	player->finish();
}

bool PacketReader::openStreamComponent(int streamId)
{
	AVFormatContext *formatContext = player->formatContext;

	// Search and open decoder
	AVCodecContext *codecContext = formatContext->streams[streamId]->codec;
	AVCodec *codec = avcodec_find_decoder(codecContext->codec_id);
	if (!codec)
	{
		player->setReadError(Player::READ_ERROR_NO_DECODER);
		return false;
	}
	AVDictionary *options = nullptr;
	av_dict_set(&options, "refcounted_frames", "1", 0);
	if (avcodec_open2(codecContext, codec, &options) != 0)
	{
		player->setReadError(Player::READ_ERROR_OPEN_CODEC);
		av_dict_free(&options);
		return false;
	}
	av_dict_free(&options);

	// Discard useless packets
	formatContext->streams[streamId]->discard = AVDISCARD_DEFAULT;

	// Ignore all other streams
	for (std::size_t i = 0; i < formatContext->nb_streams; i++)
	{
		if (i != static_cast<std::size_t>(streamId))
		{
			formatContext->streams[i]->discard = AVDISCARD_ALL;
			avcodec_close(formatContext->streams[i]->codec);
		}
	}

	int sampleRate, channelCount;
	uint64_t channelLayout;
	// TODO Work on audio filters
	if (false)
	{
		player->filterProps.frequency = codecContext->sample_rate;
		player->filterProps.channelCount = codecContext->channels;
		player->filterProps.channelLayout = Player::getValidChannelLayout(
			codecContext->channel_layout, codecContext->channels);
		player->filterProps.format = codecContext->sample_fmt;
		//AVFilterLink *link = // Create output filter
	} else
	{
		sampleRate = codecContext->sample_rate;
		channelCount = codecContext->channels;
		channelLayout = codecContext->channel_layout;
	}

	// Initialize stream related data
	player->bufferIndex = 0;
	player->bufferSize = 0;
	player->streamId = streamId;
	player->stream = formatContext->streams[streamId];

	// Initialize decoder
	player->decoder.reset(new PacketToFrameDecoder(player, codecContext));
	player->decoder->setInitalPlayTime(player->stream->time_base,
		player->stream->start_time);
	player->decodeThread = std::thread(&Player::decode, player);

	return true;
}
