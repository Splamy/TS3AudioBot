#include "ServerConnection.hpp"

#include <public_errors.h>
#include <algorithm>
#include <functional>

namespace
{
// Simple functions used to pass to standard functions
static anyID getUserId(const User *u)
{
	return u->getId();
}

static bool userIdEqual(anyID id, const User *u)
{
	return id == u->getId();
}
}

ServerConnection::ServerConnection(std::shared_ptr<TsApi> tsApi,
	uint64 handlerId, CodecType channelCodec, int channelQuality,
	bool hasGoodQuality) :
	tsApi(std::move(tsApi)),
	handlerId(handlerId),
	channelCodec(channelCodec),
	channelQuality(channelQuality),
	hasGoodQuality(hasGoodQuality),
	audioOn(false)
{
}

ServerConnection::ServerConnection(ServerConnection &&con) :
	tsApi(std::move(con.tsApi)),
	handlerId(con.handlerId),
	channelCodec(con.channelCodec),
	hasGoodQuality(con.hasGoodQuality),
	audioOn(con.audioOn),
	users(std::move(con.users)),
	whisperChannels(std::move(con.whisperChannels)),
	whisperUsers(std::move(con.whisperUsers))
{
}

ServerConnection& ServerConnection::operator = (ServerConnection &&con)
{
	tsApi = std::move(con.tsApi);
	handlerId = con.handlerId;
	channelCodec = con.channelCodec;
	hasGoodQuality = con.hasGoodQuality;
	audioOn = con.audioOn;
	users = std::move(con.users);
	whisperChannels = std::move(con.whisperChannels);
	whisperUsers = std::move(con.whisperUsers);
	return *this;
}

uint64 ServerConnection::getHandlerId() const
{
	return handlerId;
}

bool ServerConnection::shouldWhisper() const
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
			tsApi->handleTsError(tsApi->getFunctions().
				requestClientSetWhisperList(handlerId, 0, targetChannels.data(),
				targetUsers.data(), nullptr));
		} else
			// Unset whisperlist
			tsApi->handleTsError(tsApi->getFunctions().
				requestClientSetWhisperList(handlerId, 0, nullptr, nullptr, nullptr));
	}
	tsApi->handleTsError(tsApi->getFunctions().setClientSelfVariableAsInt(
		handlerId, CLIENT_INPUT_DEACTIVATED,
		on ? INPUT_ACTIVE : INPUT_DEACTIVATED));
}

void ServerConnection::setQuality(bool on)
{
	if (on != hasGoodQuality)
	{
		anyID clientId;
		uint64 channelId;
		if (!tsApi->handleTsError(tsApi->getFunctions().getClientID(handlerId,
		    &clientId)) ||
		    !tsApi->handleTsError(tsApi->getFunctions().getChannelOfClient(
		    handlerId, clientId, &channelId)))
			return;
		if (on)
		{
			// Save codec and quality
			int codec;
			if (!tsApi->handleTsError(tsApi->getFunctions().
			    getChannelVariableAsInt(handlerId, channelId, CHANNEL_CODEC,
			    &codec)))
				return;
			channelCodec = static_cast<CodecType>(codec);
			if (!tsApi->handleTsError(tsApi->getFunctions().
			    getChannelVariableAsInt(handlerId, channelId,
			    CHANNEL_CODEC_QUALITY, &channelQuality)))
				return;
		}
		tsApi->handleTsError(tsApi->getFunctions().setChannelVariableAsInt(
			handlerId, channelId, CHANNEL_CODEC,
			on ? CODEC_OPUS_MUSIC : channelCodec));
		tsApi->handleTsError(tsApi->getFunctions().setChannelVariableAsInt(
			handlerId, channelId, CHANNEL_CODEC_QUALITY,
			on ? 7 : channelQuality));
		char c;
		tsApi->handleTsError(tsApi->getFunctions().flushChannelUpdates(
			handlerId, channelId, &c));
		hasGoodQuality = on;
	}
}

User* ServerConnection::getUser(const std::string &uniqueId)
{
	for (User &user : users)
	{
		if (user.getUniqueId() == uniqueId)
			return &user;
	}
	return nullptr;
}

User* ServerConnection::getUser(uint64 dbId)
{
	for (User &user : users)
	{
		if (user.getDbId() == dbId)
			return &user;
	}
	return nullptr;
}

User* ServerConnection::getUser(anyID userId)
{
	for (User &user : users)
	{
		if (user.getId() == userId)
			return &user;
	}
	return nullptr;
}

void ServerConnection::addUser(anyID userId, const std::string &uniqueId)
{
	users.emplace_back(this, tsApi, userId, uniqueId);
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
	tsApi->handleTsError(tsApi->getFunctions().stopConnection(handlerId,
		quitMessage.c_str()));
}
