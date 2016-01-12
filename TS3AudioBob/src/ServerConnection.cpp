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
	// Pause audio playback if it's still playing
	if (audioPlayer)
	{
		if (audioOn)
		{
			if (autoPaused)
			{
				autoPaused = false;
				audioPlayer->setPaused(false);
			}
		} else if (!audioPlayer->isPaused())
		{
			autoPaused = true;
			audioPlayer->setPaused(true);
		}
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

std::vector<User*> ServerConnection::getUsers(const std::string &uniqueId)
{
	std::vector<User*> result;
	for (User &user : users)
	{
		if (user.getUniqueId() == uniqueId)
			result.push_back(&user);
	}
	return result;
}

std::vector<User*> ServerConnection::getUsers(uint64 dbId)
{
	std::vector<User*> result;
	for (User &user : users)
	{
		if (user.hasDbId() && user.getDbId() == dbId)
			result.push_back(&user);
	}
	return result;
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
	// Check if the user is already in the whisper list
	std::vector<const User*>::iterator it = std::find_if(whisperUsers.begin(),
		whisperUsers.end(), std::bind(userIdEqual, user->getId(),
		std::placeholders::_1));
	if (it == whisperUsers.end())
	{
		whisperUsers.push_back(user);
		// Update the whisper list
		setAudio(audioOn);
	}
}

void ServerConnection::addWhisperChannel(uint64 channel)
{
	// Check if the channel is already in the whisper list
	std::vector<uint64>::iterator it = std::find(whisperChannels.begin(),
		whisperChannels.end(), channel);
	if (it == whisperChannels.end())
	{
		whisperChannels.push_back(channel);
		setAudio(audioOn);
	}
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

void ServerConnection::setVolume(double volume)
{
	this->volume = volume;
	if (audioPlayer)
		audioPlayer->setVolume(volume);
}

double ServerConnection::getVolume() const
{
	return volume;
}

void ServerConnection::setLooped(bool loop)
{
	this->loop = loop;
	if (audioPlayer)
		audioPlayer->setLooped(loop);
}

bool ServerConnection::isLooped() const
{
	return loop;
}

void ServerConnection::setAudioPosition(double position)
{
	audioPlayer->setPosition(position);
}

void ServerConnection::startAudio(const std::string &address)
{
	// Load and start an audio stream
	autoPaused = false;
	audioPlayer.reset(new audio::Player(address));
	// Use default properties, the channel settings will by dynamically updated
	audioPlayer->setTargetProperties(AV_SAMPLE_FMT_S16, 48000, 2,
		AV_CH_LAYOUT_STEREO);
	audioPlayer->setVolume(volume);
	audioPlayer->setLooped(loop);
	audioPlayer->start();
}

void ServerConnection::stopAudio()
{
	audioPlayer.reset();
}

bool ServerConnection::hasAudioPlayer() const
{
	return static_cast<bool>(audioPlayer);
}

bool ServerConnection::isAudioPaused() const
{
	return audioPlayer->isPaused();
}

void ServerConnection::setAudioPaused(bool paused)
{
	audioPlayer->setPaused(paused);
	autoPaused = false;
}

bool ServerConnection::fillAudioData(uint8_t *buffer, size_t length,
	int channelCount, bool sending)
{
	if (sending && audioPlayer)
	{
		audio::AudioProperties props = audioPlayer->getTargetProperties();
		if (props.channelCount != channelCount)
		{
			props.channelCount = channelCount;
			props.channelLayout = channelCount == 2 ?
				AV_CH_LAYOUT_STEREO : AV_CH_LAYOUT_MONO;
			// Reset dynamically computed properties
			props.bytesPerSecond = 0;
			props.frameSize = 0;
			audioPlayer->setTargetProperties(props);
		}
		audioPlayer->fillBuffer(buffer, length);
		return true;
	}
	return false;
}

std::string ServerConnection::getAudioStatus() const
{
	std::ostringstream out;
	out << "\nstatus ";
	if (!audioPlayer)
		out << "off";
	else
	{
		if (audioPlayer->getReadError() != audio::Player::READ_ERROR_NONE)
			out << "error\nread error " << audio::Player::getReadErrorDescription(
				audioPlayer->getReadError());
		else if (audioPlayer->isFinished())
			out << "finished";
		else if (audioPlayer->isPaused())
			out << "paused";
		else
			out << "playing";

		if (audioPlayer->getDecodeError() != audio::Player::DECODE_ERROR_NONE)
			out << "\ndecode error " << audio::Player::getDecodeErrorDescription(
				audioPlayer->getDecodeError());

		std::string address = audioPlayer->getStreamAddress();
		Utils::replace(address, "\\", "\\\\");
		Utils::replace(address, "\n", "\\n");
		out << "\naddress " << address;

		if (audioPlayer->getReadError() == audio::Player::READ_ERROR_NONE)
		{
			out << "\nlength " << audioPlayer->getDuration();
			out << "\nposition " << audioPlayer->getPosition();
			std::unique_ptr<std::string> title = audioPlayer->getTitle();
			if (title)
			{
				Utils::replace(*title, "\\", "\\\\");
				Utils::replace(*title, "\n", "\\n");
				out << "\ntitle " << *title;
			}
		}
	}
	out << "\nloop " << (loop ? "on" : "off");
	out << "\nvolume " << volume;

	return out.str();
}

void ServerConnection::close(const std::string &quitMessage)
{
	stopAudio();
	setQuality(false);
	tsApi->handleTsError(tsApi->getFunctions().stopConnection(handlerId,
		quitMessage.c_str()));
}
