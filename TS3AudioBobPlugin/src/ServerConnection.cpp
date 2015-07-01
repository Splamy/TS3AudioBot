#include "ServerConnection.hpp"

#include <public_errors.h>
#include <algorithm>
#include <functional>

// Simple functions used to pass to standard functions
static anyID getUserId(const User *u)
{
	return u->getId();
}

static bool userIdEqual(anyID id, const User *u)
{
	return id == u->getId();
}

ServerConnection::ServerConnection(ServerBob *bob, uint64 handlerId,
	CodecType channelCodec, int channelQuality, bool hasGoodQuality) :
	handlerId(handlerId), channelCodec(channelCodec),
	channelQuality(channelQuality), hasGoodQuality(hasGoodQuality),
	audioOn(false), bob(bob)
{
}

ServerConnection::ServerConnection(ServerConnection &&con) :
	handlerId(con.handlerId),
	channelCodec(con.channelCodec),
	hasGoodQuality(con.hasGoodQuality),
	audioOn(con.audioOn),
	users(std::move(con.users)),
	whisperChannels(std::move(con.whisperChannels)),
	whisperUsers(std::move(con.whisperUsers)),
	bob(con.bob)
{
}

ServerConnection& ServerConnection::operator = (ServerConnection &&con)
{
	handlerId = con.handlerId;
	channelCodec = con.channelCodec;
	hasGoodQuality = con.hasGoodQuality;
	audioOn = con.audioOn;
	users = std::move(con.users);
	whisperChannels = std::move(con.whisperChannels);
	whisperUsers = std::move(con.whisperUsers);
	bob = con.bob;
	return *this;
}

uint64 ServerConnection::getHandlerId()
{
	return handlerId;
}

bool ServerConnection::handleTsError(unsigned int error)
{
	if (error != ERROR_ok)
	{
		char* errorMsg;
		if (bob->functions.getErrorMessage(error, &errorMsg) == ERROR_ok)
		{
			bob->log("TeamSpeak-error: %s", errorMsg);
			// Send the message to the bot
			std::string msg = errorMsg;
			bob->functions.freeMemory(errorMsg);
			Utils::replace(msg, "\n", "\\n");
		} else
			bob->log("TeamSpeak-double-error");
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
	if (on)
	{
		if (shouldWhisper())
		{
			std::vector<anyID> targetUsers(whisperUsers.size() + 1);
			std::transform(whisperUsers.cbegin(), whisperUsers.cend(),
				targetUsers.begin(), &getUserId);
			targetUsers.back() = 0;
			std::vector<uint64> targetChannels(whisperChannels);
			targetChannels.emplace_back(0);
			handleTsError(bob->functions.requestClientSetWhisperList(
				handlerId, 0, targetChannels.data(), targetUsers.data(), NULL));
		} else
			// Unset whisperlist
			handleTsError(bob->functions.requestClientSetWhisperList(
				handlerId, 0, NULL, NULL, NULL));
	}
	handleTsError(bob->functions.setClientSelfVariableAsInt(handlerId,
		CLIENT_INPUT_DEACTIVATED, on ? INPUT_ACTIVE : INPUT_DEACTIVATED));
}

void ServerConnection::setQuality(bool on)
{
	if (on != hasGoodQuality)
	{
		anyID clientId;
		uint64 channelId;
		if (!handleTsError(bob->functions.getClientID(handlerId, &clientId)) ||
		    !handleTsError(bob->functions.getChannelOfClient(handlerId,
		    clientId, &channelId)))
			return;
		if (on)
		{
			// Save codec and quality
			int codec;
			if (!handleTsError(bob->functions.getChannelVariableAsInt(handlerId,
			    channelId, CHANNEL_CODEC, &codec)))
				return;
			channelCodec = static_cast<CodecType>(codec);
			if (!handleTsError(bob->functions.getChannelVariableAsInt(handlerId,
			    channelId, CHANNEL_CODEC_QUALITY, &channelQuality)))
				return;
		}
		handleTsError(bob->functions.setChannelVariableAsInt(handlerId,
			channelId, CHANNEL_CODEC, on ? CODEC_OPUS_MUSIC : channelCodec));
		handleTsError(bob->functions.setChannelVariableAsInt(handlerId,
			channelId, CHANNEL_CODEC_QUALITY, on ? 7 : channelQuality));
		char c;
		handleTsError(bob->functions.flushChannelUpdates(handlerId, channelId, &c));
		hasGoodQuality = on;
	}
}

User* ServerConnection::getUser(const std::string &uniqueId)
{
	for (std::vector<User>::iterator it = users.begin();
	     it != users.end(); it++)
	{
		if (it->getUniqueId() == uniqueId)
			return &(*it);
	}
	return NULL;
}

User* ServerConnection::getUser(uint64 dbId)
{
	for (std::vector<User>::iterator it = users.begin();
	     it != users.end(); it++)
	{
		if (it->getDbId() == dbId)
			return &(*it);
	}
	return NULL;
}

User* ServerConnection::getUser(anyID userId)
{
	for (std::vector<User>::iterator it = users.begin();
	     it != users.end(); it++)
	{
		if (it->getId() == userId)
			return &(*it);
	}
	return NULL;
}

void ServerConnection::addUser(anyID userId, const std::string &uniqueId)
{
	users.emplace_back(this, userId, uniqueId);
}

void ServerConnection::addWhisperUser(const User *user)
{
	whisperUsers.push_back(user);
	// Update the whisper list
	setAudio(audioOn);
}

void ServerConnection::addWhisperChannel(uint64 channel)
{
	whisperChannels.push_back(channel);
	setAudio(audioOn);
}

bool ServerConnection::removeWhisperUser(const User *user)
{
	std::vector<const User*>::iterator it = std::find_if(whisperUsers.begin(),
		whisperUsers.end(), std::bind(userIdEqual, user->getId(),
		std::placeholders::_1));
	if (it == whisperUsers.end())
		return false;
	whisperUsers.erase(it);
	setAudio(audioOn);
	return true;
}

bool ServerConnection::removeWhisperChannel(uint64 channel)
{
	std::vector<uint64>::iterator it = std::find(whisperChannels.begin(),
		whisperChannels.end(), channel);
	if (it == whisperChannels.end())
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

const std::vector<const User*>* ServerConnection::getWhisperUsers() const
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
	handleTsError(bob->functions.stopConnection(handlerId, quitMessage.c_str()));
}
