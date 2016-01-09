#ifndef AUDIO_PLAYER_HPP
#define AUDIO_PLAYER_HPP

#include "AudioProperties.hpp"
#include "Frame.hpp"

extern "C"
{
	#include "libswresample/swresample.h"
}

#include <condition_variable>
#include <cstdint>
#include <memory>
#include <queue>
#include <string>
#include <thread>
#include <vector>

namespace audio
{
class PacketToFrameDecoder;

class Player
{
	friend class PacketToFrameDecoder;

public:
	enum Synchronisation
	{
		SYNC_AUDIO,
		SYNC_EXTERN
	};

private:
	std::string streamAddress;

	bool paused = false;
	bool muted = false;
	bool finished = false;
	bool error = false;
	//TODO Synchronisation sync = SYNC_AUDIO;

	std::queue<Frame> sampleQueue;
	std::mutex sampleQueueMutex;
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

	static uint64_t getValidChannelLayout(uint64_t channelLayout, int channelCount);

public:
	/** Initialize the ffmpeg library.
	 *  This has to be done before creating a player object.
	 */
	static void init();

	Player(std::string streamAddress);
	~Player();

private:
	/** The read thread that fills the packet queue. */
	void read();
	bool openStreamComponent(int streamId);
	/** The decode thread that takes packets, converts them into frames and puts
	 *  them into the sample queue.
	 */
	void decode();
	int decodeFrame();

	int computeWantedSamples(int sampleCount);

public:
	/* Getters and Setters */
	bool isPaused();
	void setPaused(bool paused);
	bool isFinished();
	bool hasErrors();
	/** Returns the duration of the audio stream in seconds.
	 *  Calling this function on an erroneous instance is undefined behaviour.
	 */
	double getDuration();
	// TODO set and apply volume

	/** Get the current properties of the audio data. */
	audio::AudioProperties getTargetProperties();
	/** Set the properties for the provided audio data. */
	bool setTargetProperties(audio::AudioProperties props);
	bool setTargetProperties(AVSampleFormat format, int frequency, int channelCount, uint64_t channelLayout, int frameSize = -1, int bytesPerSecond = -1);
	/** Start reading and decoding the stream. */
	void start();
	/** Fetch the actual audio data. */
	void fillBuffer(uint8_t *buffer, std::size_t length);
};
}

#endif
