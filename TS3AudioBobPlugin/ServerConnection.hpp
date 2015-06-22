#ifndef SERVER_CONNECTION_HPP
#define SERVER_CONNECTION_HPP

#include <ServerBob.hpp>
#include <Utils.hpp>

#include <public_definitions.h>
#include <memory>
#include <string>
#include <vector>

class ServerConnection
{
private:
	uint64 handlerID;
	CodecType channelCodec;
	int channelQuality;
	bool hasGoodQuality;
	std::vector<anyID> admins;
	ServerBob *bob;
	std::vector<uint64> whisperChannels;
	std::vector<anyID> whisperUsers;

public:
	ServerConnection(ServerBob *bob, uint64 handlerID, CodecType channelCodec = CODEC_OPUS_VOICE,
		int channelQuality = 7, bool hasGoodQuality = false);

	uint64 getHandlerID();
	bool handleTsError(unsigned int error);
	bool shouldWhisper();
	void setAudio(bool on);
	void setQuality(bool on);
	void close(const std::string &quitMessage);

	template<class... Args>
	void sendCommand(anyID userID, const std::string &message, Args... args)
	{
		handleTsError(bob->functions.requestSendPrivateTextMsg(handlerID,
			Utils::format(message, args...).c_str(), userID, NULL));
	}
	template<class... Args>
	void sendCommandAdmins(const std::string &message, Args... args)
	{
		for (std::vector<anyID>::const_iterator it = admins.cbegin(); it != admins.cend(); it++)
			sendCommand(*it, message, args...);
	}
};

#endif
