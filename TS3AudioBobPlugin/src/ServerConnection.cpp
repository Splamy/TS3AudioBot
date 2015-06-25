#include "ServerConnection.hpp"

#include <public_errors.h>

ServerConnection::ServerConnection(ServerBob *bob, uint64 handlerID,
	CodecType channelCodec, int channelQuality, bool hasGoodQuality) :
	handlerID(handlerID), channelCodec(channelCodec), channelQuality(channelQuality),
	hasGoodQuality(hasGoodQuality), audioOn(false), bob(bob)
{
	// Query connected clients
	// TODO Get database id
	//handleTsError(ts3Functions.requestClientDBIDfromUID(scHandlerID, fromUniqueIdentifier, NULL));
	// Get assigned server groups
	//handleTsError(ts3Functions.requestServerGroupsByClientID(scHandlerID, clientID, NULL));
}

uint64 ServerConnection::getHandlerID()
{
	return handlerID;
}

bool ServerConnection::handleTsError(unsigned int error)
{
	if(error != ERROR_ok)
	{
		char* errorMsg;
		if(bob->functions.getErrorMessage(error, &errorMsg) == ERROR_ok)
		{
			Utils::log("TeamSpeak-error: %s", errorMsg);
			// Send the message to the bot
			std::string msg = errorMsg;
			bob->functions.freeMemory(errorMsg);
			Utils::replace(msg, "\n", "\\n");
			// Broadcast error to all clients
			sendCommandAdmins("error %s", msg.c_str());
		} else
			Utils::log("TeamSpeak-double-error");
		return false;
	}
	return true;
}

bool ServerConnection::shouldWhisper()
{
	return !whisperChannels.empty() || !whisperUsers.empty();
}

void ServerConnection::setAudio(bool on)
{
	audioOn = on;
	if(on)
	{
		if(shouldWhisper())
		{
			std::vector<uint64> targetChannels(whisperChannels);
			targetChannels.emplace_back(0);
			std::vector<anyID> targetClients(whisperUsers);
			targetClients.emplace_back(0);
			handleTsError(bob->functions.requestClientSetWhisperList(
				handlerID, 0, targetChannels.data(), targetClients.data(), NULL));
		} else
			// Unset whisperlist
			handleTsError(bob->functions.requestClientSetWhisperList(
				handlerID, 0, NULL, NULL, NULL));
	}
	handleTsError(bob->functions.setClientSelfVariableAsInt(handlerID, CLIENT_INPUT_DEACTIVATED,
		on ? INPUT_ACTIVE : INPUT_DEACTIVATED));
}

void ServerConnection::setQuality(bool on)
{
	if(on != hasGoodQuality)
	{
		anyID clientID;
		uint64 channelID;
		if(!handleTsError(bob->functions.getClientID(handlerID, &clientID)) ||
		   !handleTsError(bob->functions.getChannelOfClient(handlerID, clientID, &channelID)))
			return;
		if(on)
		{
			// Save codec and quality
			int codec;
			if(!handleTsError(bob->functions.getChannelVariableAsInt(handlerID, channelID, CHANNEL_CODEC, &codec)))
				return;
			channelCodec = static_cast<CodecType>(codec);
			if(!handleTsError(bob->functions.getChannelVariableAsInt(handlerID, channelID, CHANNEL_CODEC_QUALITY, &channelQuality)))
				return;
		}
		handleTsError(bob->functions.setChannelVariableAsInt(handlerID, channelID, CHANNEL_CODEC,
			on ? CODEC_OPUS_MUSIC : channelCodec));
		handleTsError(bob->functions.setChannelVariableAsInt(handlerID, channelID, CHANNEL_CODEC_QUALITY,
			on ? 7 : channelQuality));
		char c;
		handleTsError(bob->functions.flushChannelUpdates(handlerID, channelID, &c));
		hasGoodQuality = on;
	}
}

void ServerConnection::addWhisperUser(anyID client)
{
	whisperUsers.push_back(client);
	setAudio(audioOn);
}

void ServerConnection::addWhisperChannel(uint64 channel)
{
	whisperChannels.push_back(channel);
	setAudio(audioOn);
}

bool ServerConnection::removeWhisperUser(anyID client)
{
	std::vector<anyID>::iterator it = std::find(whisperUsers.begin(), whisperUsers.end(), client);
	if(it == whisperUsers.end())
		return false;
	whisperUsers.erase(it);
	setAudio(audioOn);
	return true;
}

bool ServerConnection::removeWhisperChannel(uint64 channel)
{
	std::vector<uint64>::iterator it = std::find(whisperChannels.begin(), whisperChannels.end(), channel);
	if(it == whisperChannels.end())
		return false;
	whisperChannels.erase(it);
	setAudio(audioOn);
	return true;
}

void ServerConnection::clearWhisper()
{
	whisperUsers.clear();
	whisperChannels.clear();
	setAudio(audioOn);
}

const std::vector<anyID>* ServerConnection::getWhisperUsers() const
{
	return &whisperUsers;
}

const std::vector<uint64>* ServerConnection::getWhisperChannels() const
{
	return &whisperChannels;
}

void ServerConnection::close(const std::string &quitMessage)
{
	setQuality(false);
	handleTsError(bob->functions.stopConnection(handlerID, quitMessage.c_str()));
}
