#ifndef SERVER_CONNECTION_HPP
#define SERVER_CONNECTION_HPP

#include <ServerBob.hpp>
#include <User.hpp>
#include <Utils.hpp>

#include <public_definitions.h>
#include <memory>
#include <string>
#include <vector>

class ServerConnection
{
private:
	uint64 handlerId;
	CodecType channelCodec;
	int channelQuality;
	bool hasGoodQuality;
	bool audioOn;
	std::vector<User> users;
	std::vector<uint64> whisperChannels;
	std::vector<const User*> whisperUsers;

public:
	ServerBob *bob;

	ServerConnection(ServerBob *bob, uint64 handlerId,
		CodecType channelCodec = CODEC_OPUS_VOICE, int channelQuality = 7,
		bool hasGoodQuality = false);
	/** Don't copy this object. */
	ServerConnection(ServerConnection&) = delete;
	ServerConnection(ServerConnection &&con);
	ServerConnection& operator = (ServerConnection &&con);

	uint64 getHandlerId();
	bool handleTsError(unsigned int error);
	bool shouldWhisper();
	void setAudio(bool on);
	void setQuality(bool on);
	User* getUser(const std::string &uniqueId);
	User* getUser(uint64 dbId);
	User* getUser(anyID userId);
	// TODO remove users again
	void addUser(anyID id, const std::string &uniqueId);
	void addWhisperUser(const User *user);
	void addWhisperChannel(uint64 channel);
	bool removeWhisperUser(const User *user);
	bool removeWhisperChannel(uint64 channel);
	void clearWhisper();
	const std::vector<const User*>* getWhisperUsers() const;
	const std::vector<uint64>* getWhisperChannels() const;
	void close(const std::string &quitMessage);

	template <class... Args>
	void sendCommand(const User *user, const std::string &message, Args... args)
	{
		handleTsError(bob->functions.requestSendPrivateTextMsg(handlerId,
			Utils::format(message, args...).c_str(), user->getId(), NULL));
	}
};

#endif
