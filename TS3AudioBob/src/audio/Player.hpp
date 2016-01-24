#ifndef AUDIO_PLAYER_HPP
#define AUDIO_PLAYER_HPP

#include "AudioProperties.hpp"
#include "Frame.hpp"

extern "C"
{
	#include <libswresample/swresample.h>
}

#include <condition_variable>
#include <cstdint>
#include <functional>
#include <memory>
#include <queue>
#include <string>
#include <thread>
#include <vector>

namespace audio
{
class PacketReader;
class PacketToFrameDecoder;

class Player
{
	friend class PacketReader;
	friend class PacketToFrameDecoder;

	/** Maximum number of packets in the packet queue (ignored for realtime streams) */
	static const std::size_t MAX_QUEUE_SIZE;
	/** Maximum number of frames in the sample queue */
	static const int MAX_SAMPLES_QUEUE_SIZE;

public:
	/** Errors that occur mainly when opening a stream.
	 *  The occurence of one of these errors means a Player is broken.
	 */
	enum ReadError
	{
		READ_ERROR_NONE = 0,
		READ_ERROR_STREAM_OPEN,
		READ_ERROR_STREAM_INFO,
		READ_ERROR_NO_AUDIO_STREAM,
		READ_ERROR_NO_DECODER,
		READ_ERROR_OPEN_CODEC,
		READ_ERROR_IO,
		READ_ERROR_FRAME_ALLOCATION,
		READ_ERROR_COUNT
	};
	/** Decode errors can happen at each decoding, which means they can occur
	 *  only temporary.
	 */
	enum DecodeError
	{
		DECODE_ERROR_NONE = 0,
		DECODE_ERROR_CREATE_RESAMPLER,
		DECODE_ERROR_GET_BUFFER_SIZE,
		DECODE_ERROR_RESAMPLE_COMPENSATION,
		DECODE_ERROR_RESAMPLING,
		DECODE_ERROR_SEEK,
		DECODE_ERROR_COUNT
	};

	enum Synchronisation
	{
		SYNC_AUDIO,
		SYNC_EXTERN
	};

private:
	static const std::string readErrorDescription[READ_ERROR_COUNT];
	static const std::string decodeErrorDescription[DECODE_ERROR_COUNT];

	std::string streamAddress;

	bool loop = false;
	bool paused = false;
	bool muted = false;
	bool finished = false;
	ReadError readError = READ_ERROR_NONE;
	DecodeError decodeError = DECODE_ERROR_NONE;
	double volume = 1;
	//TODO Synchronisation sync = SYNC_AUDIO;
	/** If all attributes are initialized and the music player is operating
	 *  normally.
	 */
	bool initialized = false;

	std::queue<Frame> sampleQueue;
	std::mutex sampleQueueMutex;
	std::mutex readThreadMutex;
	std::condition_variable sampleQueueWaiter;
	std::queue<AVPacket> packetQueue;
	std::mutex packetQueueMutex;
	int streamId;
	AudioProperties sourceProps;
	AudioProperties filterProps;
	AudioProperties targetProps;

	SwrContext *resampler = nullptr;
	AVFormatContext *formatContext = nullptr;
	AVStream *stream = nullptr;
	AVPacket flushPacket;

	/** The reader that fills the packet queue. */
	std::unique_ptr<PacketReader> reader;
	std::unique_ptr<PacketToFrameDecoder> decoder;

	std::vector<uint8_t> localBuffer;
	uint8_t *pointer;
	std::size_t bufferIndex = 0;
	std::size_t bufferSize = 0;

	double callbackTime;
	double clockTime;
	int clockQueueId = -1;

	std::thread decodeThread;
	std::thread readThread;
	std::condition_variable readThreadWaiter;
	std::condition_variable packetQueueWaiter;
	std::condition_variable pausedWaiter;

	/* User-defined functions used as callbacks. */
	std::function<void(Player*, const std::string&)> onLog;
	std::function<void(Player*, ReadError)> onReadError;
	std::function<void(Player*, DecodeError)> onDecodeError;
	std::function<void(Player*)> onFinished;

	static uint64_t getValidChannelLayout(uint64_t channelLayout, int channelCount);
	static const char* searchEntry(const AVDictionary *dict, const char *key);

public:
	/** Initialize the ffmpeg library.
	 *  This has to be done before creating a player object.
	 */
	static void init();
	static const std::string& getReadErrorDescription(ReadError error);
	static const std::string& getDecodeErrorDescription(DecodeError error);

	Player(std::string streamAddress);
	~Player();

private:
	void setReadError(ReadError error, bool lockReadThread = true);
	void setDecodeError(DecodeError error);
	void waitUntilInitialized() const;
	/** Quit threads and set finished to true. */
	void finish(bool lockReadThread = true);

	/** The decode thread that takes packets, converts them into frames and puts
	 *  them into the sample queue.
	 */
	void decode();
	int decodeFrame();

	int computeWantedSamples(int sampleCount);

	/** Sets the current stream position as stream dependant timestamp. */
	void setPositionTime(int64_t position);
	/** Gets the current stream position as stream dependant timestamp. */
	int64_t getPositionTime() const;

public:
	/** Sets the current stream position in seconds. */
	bool setPosition(double time);
	/** Gets the current stream position in seconds. */
	double getPosition() const;
	/* Getters and Setters */
	bool isLooped() const;
	void setLooped(bool looped);
	bool isPaused() const;
	void setPaused(bool paused);
	bool isFinished() const;
	ReadError getReadError() const;
	DecodeError getDecodeError() const;
	const std::string& getStreamAddress() const;
	/** Searches for the title of the current stream.
	 *  If no title can be found, the result is null.
	 */
	std::unique_ptr<std::string> getTitle() const;
	/** Returns the duration of the audio stream in seconds.
	 *  Calling this function on an erroneous instance is undefined behaviour.
	 */
	double getDuration() const;
	double getVolume() const;
	void setVolume(double volume);

	/** Get the current properties of the audio data. */
	audio::AudioProperties getTargetProperties();
	/** Set the properties for the provided audio data. */
	bool setTargetProperties(audio::AudioProperties props);
	bool setTargetProperties(AVSampleFormat format, int frequency, int channelCount, uint64_t channelLayout, int frameSize = -1, int bytesPerSecond = -1);
	/** Start reading and decoding the stream. */
	void start();
	/** Fetch the actual audio data. */
	void fillBuffer(uint8_t *buffer, std::size_t length);

	/* Set the callbacks. */
	void setOnLog(std::function<void(Player*, const std::string&)> onLog);
	void setOnReadError(std::function<void(Player*, ReadError)> onReadError);
	void setOnDecodeError(std::function<void(Player*, DecodeError)> onDecodeError);
	void setOnFinished(std::function<void(Player*)> onFinished);
};
}

#endif
