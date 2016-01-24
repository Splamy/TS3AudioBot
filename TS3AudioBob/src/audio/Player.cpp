#include "Player.hpp"

extern "C"
{
	#include <libavfilter/avfilter.h>
	#include <libavutil/dict.h>
	#include <libavutil/time.h>
}

#include <cstring>

#include "PacketReader.hpp"
#include "PacketToFrameDecoder.hpp"

using namespace audio;

/** Maximum number of packets in the packet queue (ignored for realtime streams) */
const std::size_t Player::MAX_QUEUE_SIZE = 15 * 1024 * 1024;
/** Maximum number of frames in the sample queue */
const int Player::MAX_SAMPLES_QUEUE_SIZE = 9;

const std::string Player::readErrorDescription[] = {
	"No error",
	"Failed to open stream",
	"Failed to read stream info",
	"Can't find audio stream",
	"Failed find a suitable decoder",
	"Failed to open the codec",
	"IO error",
	"Can't allocate AVFrame"
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
	static bool initialized = false;

	if (!initialized)
	{
		initialized = true;
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

	// Initialize callback functions
	setOnLog([](Player*, const std::string &message){ 
		fprintf(stderr, message.c_str());
	});
	setOnReadError([](Player*, ReadError error){ 
		fprintf(stderr, "A read error occured: %s\n", getReadErrorDescription(error).c_str());
	});
	setOnDecodeError([](Player*, DecodeError error){ 
		fprintf(stderr, "A decode error occured: %s\n", getDecodeErrorDescription(error).c_str());
	});
}

Player::~Player()
{
	// Notify waiting threads and wait for them to exit
	finish();

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

void Player::setReadError(ReadError error, bool lockReadThread)
{
	readError = error;
	if (error != READ_ERROR_NONE)
		onReadError(this, error);
	// Read errors are fatal errors so playing sound doesn't work anymore
	finish(lockReadThread);
}

void Player::setDecodeError(DecodeError error)
{
	decodeError = error;
	if (error != DECODE_ERROR_NONE)
		onDecodeError(this, error);
}

void Player::waitUntilInitialized() const
{
	// Busy waiting because it shouldn't take long until everything is
	// initialized
	while (!initialized)
		std::this_thread::sleep_for(std::chrono::milliseconds(10));
}

void Player::finish(bool lockReadThread)
{
	// Notify waiting threads and wait for them to exit
	finished = true;
	initialized = true;
	{
		std::lock_guard<std::mutex> lock(sampleQueueMutex);
		sampleQueueWaiter.notify_all();
	}
	if (lockReadThread)
	{
		std::lock_guard<std::mutex> lock(readThreadMutex);
		readThreadWaiter.notify_all();
		pausedWaiter.notify_all();
	} else
	{
		readThreadWaiter.notify_all();
		pausedWaiter.notify_all();
	}
	{
		std::lock_guard<std::mutex> lock(packetQueueMutex);
		packetQueueWaiter.notify_all();
	}
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
		setReadError(READ_ERROR_FRAME_ALLOCATION);
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

bool Player::setPosition(double time)
{
	waitUntilInitialized();
	if (finished)
		return false;
	std::lock_guard<std::mutex> lock(readThreadMutex);
	setPositionTime(time / av_q2d(stream->time_base));
	return true;
}

double Player::getPosition() const
{
	waitUntilInitialized();
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
	waitUntilInitialized();
	if (!formatContext)
		return nullptr;
	const char *title = searchEntry(formatContext->metadata, "title");
	if (!title && stream)
		title = searchEntry(stream->metadata, "title");
	return title ? std::unique_ptr<std::string>(new std::string(title)) : nullptr;
}

double Player::getDuration() const
{
	waitUntilInitialized();
	if (!formatContext)
		return 0;
	return (double) formatContext->duration / AV_TIME_BASE;
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
	reader.reset(new PacketReader(this));
	readThread = std::thread(&PacketReader::read, reader.get());
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

void Player::setOnLog(std::function<void(Player*, const std::string&)> onLog)
{
	this->onLog = onLog;
}

void Player::setOnReadError(std::function<void(Player*, ReadError)> onReadError)
{
	this->onReadError = onReadError;
}

void Player::setOnDecodeError(std::function<void(Player*, DecodeError)> onDecodeError)
{
	this->onDecodeError = onDecodeError;
}

void Player::setOnFinished(std::function<void(Player*)> onFinished)
{
	this->onFinished = onFinished;
}
