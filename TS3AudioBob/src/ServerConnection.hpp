// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

#ifndef SERVER_CONNECTION_HPP
#define SERVER_CONNECTION_HPP

#include "audio/Player.hpp"
#include "TsApi.hpp"
#include "User.hpp"
#include "Utils.hpp"

#include <memory>
#include <string>
#include <vector>

class ServerConnection
{
private:
	std::shared_ptr<TsApi> tsApi;
	uint64 handlerId;
	CodecType channelCodec;
	int channelQuality;
	bool hasGoodQuality;
	bool audioOn;
	std::vector<std::shared_ptr<User> > users;
	std::vector<uint64> whisperChannels;
	std::vector<std::shared_ptr<User> > whisperUsers;

	// Audio player
	std::unique_ptr<audio::Player> audioPlayer;
	/** Indicates if the music player was paused automatically when the bot
	 *  was muted.
	 */
	bool autoPaused;
	double volume = 1;
	bool loop = false;

public:
	ServerConnection(std::shared_ptr<TsApi> tsApi, uint64 handlerId,
		CodecType channelCodec = CODEC_OPUS_VOICE, int channelQuality = 7,
		bool hasGoodQuality = false);
	/** Don't copy this object. */
	ServerConnection(ServerConnection&) = delete;
	ServerConnection(ServerConnection &&con);
	ServerConnection& operator = (ServerConnection &&con);

	uint64 getHandlerId() const;
	bool shouldWhisper() const;
	void setAudio(bool on);
	void setQuality(bool on);
	std::vector<std::shared_ptr<User> > getUsers(const std::string &uniqueId);
	std::vector<std::shared_ptr<User> > getUsers(uint64 dbId);
	std::shared_ptr<User> getUser(anyID userId);
	// TODO remove users again
	void addUser(anyID id, const std::string &uniqueId);
	void addWhisperUser(std::shared_ptr<User> user);
	void addWhisperChannel(uint64 channel);
	bool removeWhisperUser(std::shared_ptr<User> user);
	bool removeWhisperChannel(uint64 channel);
	void clearWhisper();
	const std::vector<std::shared_ptr<User> >* getWhisperUsers() const;
	const std::vector<uint64>* getWhisperChannels() const;

	/** Sets the volume of the audio player.
	 *  @return False will be returned if the audio player doesn't exist.
	 */
	void setVolume(double volume);
	double getVolume() const;
	void setLooped(bool loop);
	bool isLooped() const;
	bool setAudioPosition(double position);
	void startAudio(const std::string &address);
	void stopAudio();
	bool hasAudioPlayer() const;
	std::string getStreamAddress() const;
	bool isAudioPaused() const;
	/** Pauses or unpauses the audio player.
	 *  @return False will be returned if the audio player doesn't exist.
	 */
	void setAudioPaused(bool paused);
	/** Fills a buffer with audio data.
	 *  @return True, if the buffer was edited.
	 */
	bool fillAudioData(uint8_t *buffer, size_t length,
		int channelCount, bool sending);
	/** Composes a message for the current status of the audio player. */
	std::string getAudioStatus() const;

	/** Closes the connection to this server with a quit message. */
	void close(const std::string &quitMessage);

	template <class... Args>
	void sendCommand(std::shared_ptr<User> user, const std::string &message, Args... args)
	{
		std::string msg = Utils::format(message, args...);
		// Crop message if it's longer than 1024 bytes
		// Let's hope TeamSpeak won't complain if we destroy some unicode
		// Also, some characters are expanded to 2 bytes:
		static const std::string doubleChars = " \\/\n\r\f\t\v|";

		// Find out when the max length is reached
		std::string::size_type length = 0;
		std::string::size_type i = 0;
		for (; i < msg.length() && length < 1024; i++)
		{
			if (doubleChars.find(msg[i]) != std::string::npos)
				length += 2;
			else
				length++;
		}
		if (length > 1024 || i < msg.length() - 1)
		{
			// Something needs to be removed again because the last parsed
			// character takes 2 bytes
			if (length > 1024)
				i--;

			msg.erase(msg.begin() + i + 1, msg.end());
			// Append ...
			msg[i - 2] = '.';
			msg[i - 1] = '.';
			msg[i] = '.';
		}
		tsApi->handleTsError(tsApi->getFunctions().requestSendPrivateTextMsg(
			handlerId, msg.c_str(), user->getId(),
			nullptr));
	}

private:
	/* Callbacks for player events. */
	void onLog(audio::Player*, const std::string &message);
	void onReadError(audio::Player*, audio::Player::ReadError error);
	void onDecodeError(audio::Player*, audio::Player::DecodeError error);
	void onFinished(audio::Player*);
};

#endif
