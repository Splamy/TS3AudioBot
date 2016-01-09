#include "Player.hpp"

extern "C"
{
	#include <libavutil/time.h>
	#include <libavfilter/avfilter.h>
}

#ifdef __linux__
	#include <pthread.h>
#endif

#ifndef NDEBUG
	#include <cassert>
#endif
#include <cstring>

#include "PacketToFrameDecoder.hpp"

/** Maximum number of packets in the packet queue (ignored for realtime streams) */
static const int MAX_QUEUE_SIZE = 15 * 1024 * 1024;
/** Maximum number of frames in the sample queue */
static const int MAX_SAMPLES_QUEUE_SIZE = 9;

using namespace audio;

uint64_t Player::getValidChannelLayout(uint64_t channelLayout, int channelCount)
{
	if (channelLayout && av_get_channel_layout_nb_channels(channelLayout) == channelCount)
		return channelLayout;
	return 0;
}

void Player::init()
{
	// Initialize ffmpeg only once
	static bool inited = false;

	if (!inited)
	{
		inited = true;
		av_log_set_flags(AV_LOG_SKIP_REPEATED);
		avfilter_register_all();
		av_register_all();
		avformat_network_init();
	}
}

Player::Player(std::string streamAddress) :
	streamAddress(streamAddress)
{
	// Init the flush packet
	av_init_packet(&flushPacket);
	flushPacket.data = reinterpret_cast<uint8_t*>(&flushPacket);
	packetQueue.push(flushPacket);

	// Set default target properties
	// Signed 16 bit integers, packed (non-planar); 44100 Hz; 2 Channels; Stereo
	setTargetProperties(AV_SAMPLE_FMT_S16, 44100, 2, AV_CH_LAYOUT_STEREO);
}

Player::~Player()
{
	// Notify waiting threads and wait for them to exit
	finished = true;
	sampleQueueWaiter.notify_all();
	readThreadWaiter.notify_all();
	packetQueueWaiter.notify_all();
	if (readThread.joinable())
		readThread.join();
	if (decodeThread.joinable())
		decodeThread.join();

	swr_free(&resampler);
	if (formatContext)
	{
		if (streamId != -1)
			avcodec_close(formatContext->streams[streamId]->codec);
		avformat_close_input(&formatContext);
	}
}

void Player::read()
{
	// Set thread name if available
#ifdef __linux__
	pthread_setname_np(pthread_self(), "ReadThread");
#endif

	if (avformat_open_input(&formatContext, streamAddress.c_str(), nullptr, nullptr) != 0)
	{
		// TODO Handle logging better (use a logger) and exit better
		av_log(nullptr, AV_LOG_FATAL, "Can't open stream");
		finished = true;
		error = true;
		return;
	}
	av_format_inject_global_side_data(formatContext);
	if (avformat_find_stream_info(formatContext, nullptr) < 0)
	{
		av_log(nullptr, AV_LOG_FATAL, "Can't find stream info");
		avformat_close_input(&formatContext);
		finished = true;
		error = true;
		return;
	}

	// Print audio stream information
	av_dump_format(formatContext, 0, streamAddress.c_str(), 0);

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
		av_log(nullptr, AV_LOG_FATAL, "Can't find audio stream");
		avformat_close_input(&formatContext);
		finished = true;
		error = true;
		return;
	}

	if (!openStreamComponent(audioStreamId))
	{
		finished = true;
		error = true;
		return;
	}

	// Read the stream
	bool lastPaused = false;
	// The mutex and lock to wait with the readThreadWaiter
	std::mutex waitMutex;
	std::unique_lock<std::mutex> waitLock(waitMutex);
	AVPacket packet;
	bool hasEof = false;
	while (!finished)
	{
		if (paused != lastPaused)
		{
			lastPaused = paused;
			if (paused)
				av_read_pause(formatContext);
			else
				av_read_play(formatContext);
		}
		//FIXME It seems like there is a workaround needed for some formats
		if (paused && (strcmp(formatContext->iformat->name, "rtsp") == 0 ||
					   (formatContext->pb && strncmp(formatContext->filename, "mmsh:", 5) == 0)))
		{
			std::this_thread::sleep_for(std::chrono::milliseconds(10));
			continue;
		}

		// Stop reading if the queue is full and if it's not a realtime stream
		if (!realtime && packetQueue.size() >= MAX_QUEUE_SIZE)
		{
			readThreadWaiter.wait_for(waitLock, std::chrono::milliseconds(10));
			continue;
		}

		// Test if the stream is over
		if (!paused && packetQueue.empty())
		{
			finished = true;
			return;
		}

		// Read a packet from the stream
		int ret = av_read_frame(formatContext, &packet);
		if (ret < 0)
		{
			if ((ret == AVERROR_EOF || avio_feof(formatContext->pb)) && !hasEof)
			{
				packetQueue.push(flushPacket);
				packetQueueWaiter.notify_one();
				hasEof = true;
			}
			if (formatContext->pb && formatContext->pb->error)
			{
				finished = true;
				error = true;
				return;
			}

			// Wait for more data
			readThreadWaiter.wait_for(waitLock, std::chrono::milliseconds(10));
			continue;
		} else
			hasEof = 1;

		// Insert the packet into the queue
		std::unique_lock<std::mutex> packetQueueLock(packetQueueMutex);
		packetQueue.push(packet);
		packetQueueWaiter.notify_one();
	}
}

bool Player::openStreamComponent(int streamId)
{
	// Search and open decoder
	AVCodecContext *codecContext = formatContext->streams[streamId]->codec;
	AVCodec *codec = avcodec_find_decoder(codecContext->codec_id);
	if (!codec)
	{
		av_log(nullptr, AV_LOG_FATAL, "Can't find a decoder for codec %d", codecContext->codec_id);
		return false;
	}
	if (avcodec_open2(codecContext, codec, nullptr) != 0)
	{
		av_log(nullptr, AV_LOG_FATAL, "Can't open codec");
		return false;
	}

	// Discard useless packets
	formatContext->streams[streamId]->discard = AVDISCARD_DEFAULT;

	int sampleRate, channelCount;
	uint64_t channelLayout;
	// TODO Work on audio filters
	if (false)
	{
		filterProps.frequency = codecContext->sample_rate;
		filterProps.channelCount = codecContext->channels;
		filterProps.channelLayout = getValidChannelLayout(codecContext->channel_layout, codecContext->channels);
		filterProps.format = codecContext->sample_fmt;
		//AVFilterLink *link = // Create output filter
	} else
	{
		sampleRate = codecContext->sample_rate;
		channelCount = codecContext->channels;
		channelLayout = codecContext->channel_layout;
	}

	// Initialize stream related data
	bufferIndex = 0;
	bufferSize = 0;
	this->streamId = streamId;
	stream = formatContext->streams[streamId];

	// Initialize decoder
	decoder.reset(new PacketToFrameDecoder(this, codecContext));
	decodeThread = std::thread(&Player::decode, this);

	return true;
}

void Player::decode()
{
	// Set thread name if available
#ifdef __linux__
	pthread_setname_np(pthread_self(), "DecodeThread");
#endif

	AVFrame *frame = av_frame_alloc();
	frame->extended_data = nullptr;
	if (!frame)
	{
		printf("Can't allocate a frame\n");
		return;
	}
	AVRational timeBase;
	for (;;)
	{
		int frameSize = decoder->fillFrame(frame);
		if (frameSize < 0)
			break;
		if (frameSize != 0)
		{
			timeBase = AVRational{ 1, frame->sample_rate };
			//TODO audio filter
			if (false)
			{
#if 0
				uint64_t channelLayout = getValidChannelLayout(frame->channel_layout, av_frame_get_channels(frame));
				if (compareAudioFormats(filterProps.format, filterProps.channelCount, (AVSampleFormat) frame->format, av_frame_get_channels(frame)) ||
					filterProps.channelLayout != channelLayout ||
					filterProps.frequency != frame->sample_rate)
				{
					printf("Audio changed\n");
				}
#endif
			} else
			{
				std::unique_lock<std::mutex> sampleQueueLock(sampleQueueMutex);
				// Wait until there is free space in the sample queue
				if (sampleQueue.size() >= MAX_SAMPLES_QUEUE_SIZE)
					sampleQueueWaiter.wait(sampleQueueLock, [this]{ return sampleQueue.size() < MAX_SAMPLES_QUEUE_SIZE || finished; });
				if (finished)
					break;

				// Insert a new frame into the sample queue
				Frame f(decoder->getLastQueueId());
				f.setPlayTime(frame->pts == AV_NOPTS_VALUE ? NAN : frame->pts * av_q2d(timeBase));
				f.setDuration(av_q2d(AVRational{ frame->nb_samples, frame->sample_rate }));

				av_frame_move_ref(f.getInternalFrame(), frame);
				frame->extended_data = nullptr;
				sampleQueue.push(std::move(f));
				sampleQueueWaiter.notify_one();
			}
		}
	}
	av_frame_free(&frame);
}

int Player::decodeFrame()
{
	int dataSize, resampledSize;
	uint64_t channelLayout;
	int wantedSampleCount;
	Frame frame;

	// Get a frame
	do
	{
		std::unique_lock<std::mutex> lock(sampleQueueMutex);
		if (sampleQueue.empty())
			sampleQueueWaiter.wait(lock,
				[this]{ return !sampleQueue.empty() || finished || paused; });
		if (finished || paused)
			return -1;
		frame = std::move(sampleQueue.front());
		sampleQueue.pop();
		sampleQueueWaiter.notify_one();
	} while (frame.getQueueId() != streamId);

	dataSize = av_samples_get_buffer_size(nullptr, frame.getChannelCount(),
		frame.getSampleCount(), frame.getAudioFormat(), 1);

	channelLayout = (frame.getChannelLayout() &&
			frame.getChannelCount() ==
				av_get_channel_layout_nb_channels(frame.getChannelLayout())) ?
		frame.getChannelLayout() :
			av_get_default_channel_layout(frame.getChannelCount());

	wantedSampleCount = computeWantedSamples(frame.getSampleCount());

	// Reload the resampler if the format changed
	if (frame.getAudioFormat() != sourceProps.format ||
		channelLayout          != sourceProps.channelLayout ||
		frame.getSampleRate()  != sourceProps.frequency ||
		(wantedSampleCount     != frame.getSampleCount() && !resampler))
	{
		swr_free(&resampler);
		resampler = swr_alloc_set_opts(nullptr,
			targetProps.channelLayout, targetProps.format, targetProps.frequency,
			channelLayout, frame.getAudioFormat(), frame.getSampleRate(),
			0, nullptr);
		if (!resampler || swr_init(resampler) < 0)
		{
			av_log(nullptr, AV_LOG_ERROR,
			   "Cannot create sample rate converter for conversion of %d Hz"
			   " %s %d channels to %d Hz %s %d channels!\n",
				frame.getSampleRate(),
				av_get_sample_fmt_name(frame.getAudioFormat()),
				frame.getChannelCount(),
				targetProps.frequency,
				av_get_sample_fmt_name(targetProps.format),
				targetProps.channelCount);
			swr_free(&resampler);
			return -1;
		}
		sourceProps.channelLayout = channelLayout;
		sourceProps.channelCount = frame.getChannelCount();
		sourceProps.frequency = frame.getSampleRate();
		sourceProps.format = frame.getAudioFormat();
	}

	if (resampler)
	{
		// Resample the audio data
		int outCount = (int64_t) wantedSampleCount * targetProps.frequency /
			frame.getSampleRate() + 256;
		int outSize  = av_samples_get_buffer_size(nullptr,
			targetProps.channelCount, outCount, targetProps.format, 0);
		if (outSize < 0)
		{
			av_log(nullptr, AV_LOG_ERROR, "av_samples_get_buffer_size() failed\n");
			return -1;
		}

		// Adjust sample count if we should play faster or slower
		if (wantedSampleCount != frame.getSampleCount())
		{
			if (swr_set_compensation(resampler,
				(wantedSampleCount - frame.getSampleCount()) *
					targetProps.frequency / frame.getSampleRate(),
				wantedSampleCount * targetProps.frequency / frame.getSampleRate()) < 0)
			{
				av_log(nullptr, AV_LOG_ERROR, "swr_set_compensation() failed\n");
				return -1;
			}
		}

		// Resize the temporary sample buffer
		localBuffer.resize(outSize);
		uint8_t *localBufferData = localBuffer.data();
		// Resample the data
		int length = swr_convert(resampler, &localBufferData, outCount,
			const_cast<const uint8_t**>(frame.getData()), frame.getSampleCount());
		if (length < 0)
		{
			av_log(nullptr, AV_LOG_ERROR, "swr_convert() failed\n");
			return -1;
		}
		if (length == outCount)
		{
			av_log(nullptr, AV_LOG_WARNING, "audio buffer is probably too small\n");
			if (swr_init(resampler) < 0)
				swr_free(&resampler);
		}
		pointer = localBuffer.data();
		resampledSize = length * targetProps.channelCount *
			av_get_bytes_per_sample(targetProps.format);
	} else
	{
		pointer = frame.getInternalFrame()->data[0];
		resampledSize = dataSize;
	}

	// Update the audio clock
	if (!isnan(frame.getPlayTime()))
		clockTime = frame.getPlayTime() + (double) frame.getSampleCount() / frame.getSampleRate();
	else
		clockTime = NAN;
	clockQueueId = frame.getQueueId();
	return resampledSize;
}

int Player::computeWantedSamples(int sampleCount)
{
	int wantedSamples = sampleCount;

	//TODO Work on syncing the audio to an external clock

	return wantedSamples;
}

bool Player::isPaused()
{
	return paused;
}

void Player::setPaused(bool paused)
{
	this->paused = paused;
	sampleQueueWaiter.notify_all();
}

bool Player::isFinished()
{
	return finished;
}

bool Player::hasErrors()
{
	return error;
}

double Player::getDuration()
{
	return (double) stream->duration * stream->time_base.num /
		stream->time_base.den;
}

audio::AudioProperties Player::getTargetProperties()
{
	return targetProps;
}

bool Player::setTargetProperties(AudioProperties props)
{
	if (props.frameSize <= 0)
		props.frameSize = av_samples_get_buffer_size(nullptr, props.channelCount, 1, props.format, 1);
	if (props.bytesPerSecond <= 0)
		props.bytesPerSecond = av_samples_get_buffer_size(nullptr, props.channelCount,
			props.frequency, props.format, 1);
	if (props.frameSize <= 0 || props.bytesPerSecond <= 0)
		return false;

	targetProps = props;
	sourceProps = targetProps;
	return true;
}

bool Player::setTargetProperties(AVSampleFormat format, int frequency, int channelCount, uint64_t channelLayout, int frameSize, int bytesPerSecond)
{
	AudioProperties props;
	props.format = format;
	props.frequency = frequency;
	props.channelCount = channelCount;
	props.channelLayout = channelLayout;
	props.frameSize = frameSize;
	props.bytesPerSecond = bytesPerSecond;
	return setTargetProperties(props);
}

void Player::start()
{
	readThread = std::thread(&Player::read, this);
}

void Player::fillBuffer(uint8_t *buffer, std::size_t length)
{
	callbackTime = av_gettime_relative();

	while (length > 0)
	{
		bool silence = muted;
		if (bufferIndex >= bufferSize)
		{
			int size = decodeFrame();
			if (size < 0)
			{
				// An error occured -> output silence and try to load data from
				// the next frame
				silence = true;
				bufferSize = targetProps.frameSize;
			} else
				bufferSize = size;
			bufferIndex = 0;
		}
		std::size_t len = bufferSize - bufferIndex;
		if (len > length)
			len = length;
		if (silence)
			memset(buffer, 0, len);
		else
			memcpy(buffer, pointer + bufferIndex, len);
		length -= len;
		buffer += len;
		bufferIndex += len;
	}
	if (!isnan(clockTime))
	{
		// FIXME How exactly does this code work? It's needed for funky mode ;)
		//int writeBufferSize = bufferSize - bufferIndex;
		//clock.setClockAt(clockTime - (double) (2 * hardwareBufferSize + writeBufferSize) / targetProps.bytesPerSecond, clockQueueId, callbackTime / 1000000.0);
		//externClock.syncTo(clock);
	}
}
