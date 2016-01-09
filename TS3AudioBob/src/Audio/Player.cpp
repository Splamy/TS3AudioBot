#include "Player.hpp"

extern "C"
{
	#include <libavfilter/avfilter.h>
	#include <libavutil/dict.h>
	#include <libavutil/time.h>
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

const std::string Player::readErrorDescription[] = {
	"No error",
	"Failed to open stream",
	"Failed to read stream info",
	"Can't find audio stream",
	"Failed find a suitable decoder",
	"Failed to open the codec",
	"IO error"
};
const std::string Player::decodeErrorDescription[] = {
	"No error",
	"Failed to create resampler",
	"Failed to get buffer size",
	"Failed to set compensation for resampling",
	"Failed to resample",
	"Failed to change the position"
};

uint64_t Player::getValidChannelLayout(uint64_t channelLayout, int channelCount)
{
	if (channelLayout && av_get_channel_layout_nb_channels(channelLayout) == channelCount)
		return channelLayout;
	return 0;
}

const char* Player::searchEntry(const AVDictionary *dict, const char *key)
{
	AVDictionaryEntry *entry = av_dict_get(dict, key, nullptr, 0);
	return entry ? entry->value : nullptr;
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

const std::string& Player::getReadErrorDescription(ReadError error)
{
	return readErrorDescription[error];
}

const std::string& Player::getDecodeErrorDescription(DecodeError error)
{
	return decodeErrorDescription[error];
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
	pausedWaiter.notify_all();
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

void Player::setReadError(ReadError error)
{
	readError = error;
	if (error != READ_ERROR_NONE)
		printf("A read error occured: %s\n", getReadErrorDescription(error).c_str());
	// Read errors are fatal errors so playing sound doesn't work anymore
	finished = true;
}

void Player::setDecodeError(DecodeError error)
{
	decodeError = error;
	if (error != DECODE_ERROR_NONE)
		printf("A decode error occured: %s\n", getDecodeErrorDescription(error).c_str());
}

void Player::read()
{
	// Set thread name if available
#ifdef __linux__
	pthread_setname_np(pthread_self(), "ReadThread");
#endif

	if (avformat_open_input(&formatContext, streamAddress.c_str(), nullptr, nullptr) != 0)
	{
		setReadError(READ_ERROR_STREAM_OPEN);
		return;
	}
	av_format_inject_global_side_data(formatContext);
	if (avformat_find_stream_info(formatContext, nullptr) < 0)
	{
		// Free memory
		avformat_close_input(&formatContext);
		setReadError(READ_ERROR_STREAM_INFO);
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
		avformat_close_input(&formatContext);
		setReadError(READ_ERROR_NO_AUDIO_STREAM);
		return;
	}

	if (!openStreamComponent(audioStreamId))
		return;

	// Read the stream
	bool lastPaused = false;
	// The mutex and lock to wait with the readThreadWaiter
	std::unique_lock<std::mutex> waitLock(readThreadMutex);
	AVPacket packet;
	bool hasEof = false;
	while (!finished)
	{
		if (paused != lastPaused && !realtime)
		{
			lastPaused = paused;
			if (paused)
			{
				av_read_pause(formatContext);
				pausedWaiter.wait(waitLock, [this]{ return !paused || finished; });
			} else
				av_read_play(formatContext);
		}

		// Stop reading if the queue is full and if it's not a realtime stream
		if (!realtime && (packetQueue.size() >= MAX_QUEUE_SIZE || paused))
		{
			readThreadWaiter.wait_for(waitLock, std::chrono::milliseconds(10));
			continue;
		}

		// Test if the stream is over
		if (!paused && packetQueue.empty() && !decoder->gotFlush())
		{
			if (loop)
				setPositionTime(0);
			else
			{
				finished = true;
				return;
			}
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
				setReadError(READ_ERROR_IO);
				return;
			}

			// Wait for more data
			readThreadWaiter.wait_for(waitLock, std::chrono::milliseconds(10));
			continue;
		} else
			hasEof = 1;

		// Insert the packet into the queue
		std::lock_guard<std::mutex> packetQueueLock(packetQueueMutex);
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
		setReadError(READ_ERROR_NO_DECODER);
		return false;
	}
	if (avcodec_open2(codecContext, codec, nullptr) != 0)
	{
		setReadError(READ_ERROR_OPEN_CODEC);
		return false;
	}

	// Discard useless packets
	formatContext->streams[streamId]->discard = AVDISCARD_DEFAULT;

	// Ignore all other streams
	for (size_t i = 0; i < formatContext->nb_streams; i++)
	{
		if (i != static_cast<size_t>(streamId))
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
	decoder->setInitalPlayTime(stream->time_base, stream->start_time);
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

	setDecodeError(DECODE_ERROR_NONE);
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
			setDecodeError(DECODE_ERROR_CREATE_RESAMPLER);
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
			setDecodeError(DECODE_ERROR_GET_BUFFER_SIZE);
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
				setDecodeError(DECODE_ERROR_RESAMPLE_COMPENSATION);
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
			setDecodeError(DECODE_ERROR_RESAMPLING);
			return -1;
		}
		if (length == outCount)
		{
			// Audio buffer is probably too small
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

void Player::setPositionTime(int64_t position)
{
	if (avformat_seek_file(formatContext, streamId, position, position,
		position, 0) < 0)
		setDecodeError(DECODE_ERROR_SEEK);
	else
	{
		std::lock_guard<std::mutex> lock(packetQueueMutex);
		// Flush packet queue
		while (!packetQueue.empty())
			packetQueue.pop();
		packetQueue.push(flushPacket);
	}
}

int64_t Player::getPositionTime() const
{
	return 0; //TODO
}

void Player::setPosition(double time)
{
	std::lock_guard<std::mutex> lock(readThreadMutex);
	setPositionTime(time / av_q2d(stream->time_base));
}

double Player::getPosition() const
{
	return 0; //TODO
}

bool Player::isLooped() const
{
	return loop;
}

void Player::setLooped(bool loop)
{
	this->loop = loop;
}

bool Player::isPaused() const
{
	return paused;
}

void Player::setPaused(bool paused)
{
	this->paused = paused;
	sampleQueueWaiter.notify_all();
	pausedWaiter.notify_all();
}

bool Player::isFinished() const
{
	return finished;
}

Player::ReadError Player::getReadError() const
{
	return readError;
}

Player::DecodeError Player::getDecodeError() const
{
	return decodeError;
}

const std::string& Player::getStreamAddress() const
{
	return streamAddress;
}

std::unique_ptr<std::string> Player::getTitle() const
{
	const char *title = searchEntry(formatContext->metadata, "title");
	return title ? std::unique_ptr<std::string>(new std::string(title)) : nullptr;
}

double Player::getDuration() const
{
	return stream->duration * av_q2d(stream->time_base);
}

double Player::getVolume() const
{
	return volume;
}

void Player::setVolume(double volume)
{
	this->volume = volume;
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

	std::size_t todoLength = length;
	uint8_t *curBuffer = buffer;
	while (todoLength > 0)
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
		if (len > todoLength)
			len = todoLength;
		if (silence)
			memset(curBuffer, 0, len);
		else
			memcpy(curBuffer, pointer + bufferIndex, len);
		todoLength -= len;
		curBuffer += len;
		bufferIndex += len;
	}

	// Apply current volume
	if (volume != 1)
	{
		int16_t *samples = reinterpret_cast<int16_t*>(buffer);
		std::size_t sampleCount = length / sizeof(int16_t);
		for (std::size_t i = 0; i < sampleCount; i++)
			samples[i] *= volume;
	}

	if (!isnan(clockTime))
	{
		// FIXME How exactly does this code work? It's needed for funky mode ;)
		//int writeBufferSize = bufferSize - bufferIndex;
		//clock.setClockAt(clockTime - (double) (2 * hardwareBufferSize + writeBufferSize) / targetProps.bytesPerSecond, clockQueueId, callbackTime / 1000000.0);
		//externClock.syncTo(clock);
	}
}
