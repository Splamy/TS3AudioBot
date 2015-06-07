#include <cstdio>
#include <fstream>
#include <string>
#include <vector>

#include "public_errors.h"
#include "ts3_functions.h"
#include "plugin.hpp"

static struct TS3Functions ts3Functions;
#define PATH_SIZE 1024
// Activate this to allow control messages from everyong
//#define UNSECURE

struct Server
{
	uint64 handlerID;
	CodecType channelCodec;
	int channelQuality;
	bool hasGoodQuality;

	Server(uint64 handlerID, CodecType channelCodec = CODEC_OPUS_VOICE,
		int channelQuality = 7, bool hasGoodQuality = false) :
		handlerID(handlerID), channelCodec(channelCodec), channelQuality(channelQuality),
		hasGoodQuality(hasGoodQuality)
	{
	}
};

// Attributes
static const std::string FILENAME = "queryId";
static const std::vector<std::string> quitMessages = { "I'm outta here", "You're boring", "Have a nice day", "Bye" };
static std::vector<uint64> whisperChannels;
static std::vector<anyID> whisperUsers;
static bool useWhispering = true;
static bool audioOn = false;
static std::vector<Server> servers;
static anyID serverBotId = 0;

// Methods declarations
template<typename... Args>
static void sendCommand(uint64 handlerID, const char *message, Args... args);
template<typename... Args>
static void sendCommand(uint64 handlerID, std::string message, Args... args);
template<typename... Args>
static void sendCommand(const char *message, Args... args);
template<typename... Args>
static void sendCommand(std::string message, Args... args);

// Method implementations
static std::string& replace(std::string &input, const std::string &target, const std::string &replacement)
{
	std::size_t pos;
	while((pos = input.find(target)) != std::string::npos)
		input.replace(pos, target.size(), replacement);
	return input;
}

bool startsWith(const std::string &string, const std::string &prefix)
{
	return prefix.size() <= string.size() && std::equal(prefix.begin(), prefix.end(), string.begin());
}

template<typename... Args>
static void log(const char *format, Args... args)
{
	printf(format, args...);
	printf("\n");
}

template<typename... Args>
static void log(std::string format, Args... args)
{
	log(format.c_str(), args...);
}

static bool handleTsError(unsigned int error)
{
	if(error != ERROR_ok)
	{
		char* errorMsg;
		if(ts3Functions.getErrorMessage(error, &errorMsg) == ERROR_ok)
		{
			log("TeamSpeak-error: %s", errorMsg);
			// Send the message to the bot
			std::string msg = errorMsg;
			ts3Functions.freeMemory(errorMsg);
			replace(msg, "\n", "\\n");
			sendCommand("error %s", msg.c_str());
		} else
			log("TeamSpeak-double-error");
		return false;
	}
	return true;
}

// max is not contained in the result range
static int getRandomNumber(int min, int max)
{
	// Generate random number
	//std::random_device random;
	//std::mt19937 generator(random());
	//std::uniform_int_distribution<int>  uniform(min, max);
	//return uniform(generator);
	return rand() % (max - min) + min;
}


// Library functions
static void switchAudio(bool on)
{
	std::vector<uint64> targets(whisperChannels);
	targets.emplace_back(0);
	std::vector<anyID> targetClients(whisperUsers);
	targetClients.emplace_back(0);
	for(std::vector<Server>::const_iterator it = servers.cbegin(); it != servers.cend(); it++)
	{
		if(on)
		{
			if(useWhispering)
				handleTsError(ts3Functions.requestClientSetWhisperList(
					it->handlerID, 0, targets.data(), targetClients.data(), NULL));
			else
				// Unset whisperlist
				handleTsError(ts3Functions.requestClientSetWhisperList(
					it->handlerID, 0, NULL, NULL, NULL));
		}
		handleTsError(ts3Functions.setClientSelfVariableAsInt(it->handlerID, CLIENT_INPUT_DEACTIVATED,
			on ? INPUT_ACTIVE : INPUT_DEACTIVATED));
	}
	audioOn = on;
}

static void switchQuality(bool on)
{
	for(std::vector<Server>::iterator it = servers.begin(); it != servers.end(); it++)
	{
		if(on != it->hasGoodQuality)
		{
			anyID clientID;
			uint64 channelID;
			if(!handleTsError(ts3Functions.getClientID(it->handlerID, &clientID)) ||
				handleTsError(ts3Functions.getChannelOfClient(it->handlerID, clientID, &channelID)))
				continue;
			if(on)
			{
				// Save codec and quality
				int codec;
				if(handleTsError(ts3Functions.getChannelVariableAsInt(it->handlerID, channelID, CHANNEL_CODEC, &codec)))
				{
					it->channelCodec = static_cast<CodecType>(codec);
					if(!handleTsError(ts3Functions.getChannelVariableAsInt(it->handlerID, channelID, CHANNEL_CODEC_QUALITY, &it->channelQuality)))
						continue;
				} else
					continue;
			}
			handleTsError(ts3Functions.setChannelVariableAsInt(it->handlerID, channelID, CHANNEL_CODEC,
				on ? CODEC_OPUS_MUSIC : it->channelCodec));
			handleTsError(ts3Functions.setChannelVariableAsInt(it->handlerID, channelID, CHANNEL_CODEC_QUALITY,
				on ? 7 : it->channelQuality));
			char c;
			handleTsError(ts3Functions.flushChannelUpdates(it->handlerID, channelID, &c));
			it->hasGoodQuality = on;
		}
	}
}

static void closeBob()
{
	switchQuality(false);
	std::string msg = quitMessages[getRandomNumber(0, quitMessages.size())];
	for(std::vector<Server>::const_iterator it = servers.cbegin(); it != servers.cend(); it++)
		handleTsError(ts3Functions.stopConnection(it->handlerID, msg.c_str()));
	// "Graceful" exit
	exit(0);
}

template<typename... Args>
static void sendCommand(uint64 handlerID, const char *message, Args... args)
{
	if (serverBotId == 0)
	{
		log("The serverbot id is unknown :( Tried to write the following:");
		log(message, args...);
	} else
	{
		// Create string
		std::vector<char> buf(1 + std::snprintf(NULL, 0, message, args...));
		std::snprintf(buf.data(), buf.size(), message, args...);
		handleTsError(ts3Functions.requestSendPrivateTextMsg(handlerID, buf.data(), serverBotId, NULL));
	}
}

template<typename... Args>
static void sendCommand(uint64 handlerID, std::string message, Args... args)
{
	sendCommand(handlerID, message.c_str(), args...);
}

template<typename... Args>
static void sendCommand(std::string message, Args... args)
{
	for(std::vector<Server>::const_iterator it = servers.cbegin(); it != servers.cend(); it++)
		sendCommand(it->handlerID, message, args...);
}

template<typename... Args>
static void sendCommand(const char *message, Args... args)
{
	for(std::vector<Server>::const_iterator it = servers.cbegin(); it != servers.cend(); it++)
		sendCommand(it->handlerID, message, args...);
}

static void handleCommand(std::string cmd)
{
	if(cmd == "exit")
		closeBob();
	else if(cmd == "help")
	{
		//TODO send more
		sendCommand("help \n\taudio [on|off]\n\tquality [on|off]\n\twhisper [on|off]\n\twhisper clear");
	} else if(cmd == "audio on")
		switchAudio(true);
	else if(cmd == "audio off")
		switchAudio(false);
	else if(cmd == "quality on")
		switchQuality(true);
	else if(cmd == "quality off")
		switchQuality(false);
	else if(cmd == "whisper on")
		useWhispering = true;
	else if(cmd == "whisper off")
		useWhispering = false;
	else if(cmd == "whisper clear")
	{
		whisperChannels.clear();
		whisperUsers.clear();
	} else if(startsWith(cmd, "whisper channel"))
	{
		uint64 id;
		if(std::sscanf(cmd.c_str(), "whisper channel %lu", &id) == 1)
			whisperChannels.emplace_back(id);
	} else if(startsWith(cmd, "whisper client"))
	{
		anyID id;
		if(std::sscanf(cmd.c_str(), "whisper client %hu", &id) == 1)
			whisperUsers.emplace_back(id);
	} else if(cmd == "status audio")
		sendCommand("status audio %s", audioOn ? "on" : "off");
	else if(cmd == "status whisper")
	{
		sendCommand("status whisper %s", useWhispering ? "on" : "off");
		// TODO write clients and channels
	} else if(cmd == "unknown command")
	{
		log("Loop detected, have fun");
	} else
	{
		log("Unknown command");
		sendCommand("unknown command");
	}
}


// Required functions

// Unique name of this plugin
const char* ts3plugin_name()
{
	return "TS3AudioBobPlugin";
}

// Version of this plugin
const char* ts3plugin_version()
{
	return "1.0";
}

// API version of this plugin
int ts3plugin_apiVersion()
{
	return 20;
}

// Author of this plugin
const char* ts3plugin_author()
{
	return "Sebi";
}

// Description of this plugin
const char* ts3plugin_description()
{
	return "Let the TS3AudioBot control the TS3AudioBobPlugin.";
}

// Set the callback function pointers
void ts3plugin_setFunctionPointers(const struct TS3Functions funcs)
{
	ts3Functions = funcs;
}

// Initialize the plugin
// Return 0 on success or 1 if an error occurs
int ts3plugin_init()
{
	// Get currently active connections
	uint64 *handlerIDs;
	if(!handleTsError(ts3Functions.getServerConnectionHandlerList(&handlerIDs)))
		return 1;
	for(uint64 *handlerID = handlerIDs; *handlerID != 0; handlerID++)
		servers.emplace_back(*handlerID);
	ts3Functions.freeMemory(handlerIDs);

	switchAudio(audioOn);

	// App and Resources path are empty for a console client
	// We take all of them with a priority
	std::vector<char[PATH_SIZE]> paths(4);
	ts3Functions.getAppPath(paths[0], PATH_SIZE);
	ts3Functions.getResourcesPath(paths[1], PATH_SIZE);
	ts3Functions.getPluginPath(paths[2], PATH_SIZE);
	ts3Functions.getConfigPath(paths[3], PATH_SIZE);

	// Get the server query id from a file
	for(std::vector<char[PATH_SIZE]>::const_iterator it = paths.cbegin(); it != paths.end(); it++)
	{
		std::ifstream in(std::string(*it) + "/" + FILENAME);
		int id;
		if(in)
			in >> id;
		if(in)
		{
			// Successfully read id
			serverBotId = id;
			break;
		}
	}
	if(serverBotId == 0)
	{
		log("Query id file not found, aborting");
		closeBob();
	}

	return 0;
}

// Unload the plugin
void ts3plugin_shutdown()
{
}

// Optional functions
// Returns 1 if the plugin should be autoloaded, 0 otherwise
int ts3plugin_requestAutoload()
{
	return 1;
}

// Callbacks executed by the TeamSpeak when an event occurs
void ts3plugin_onConnectStatusChangeEvent(uint64 scHandlerID, int newStatus, unsigned int errorNumber)
{
	switch(newStatus)
	{
	case STATUS_DISCONNECTED:
	{
		for (auto pos = servers.begin(); pos != servers.end(); pos++)
		{
			if(pos->handlerID == scHandlerID)
			{
				servers.erase(pos);
				break;
			}
		}
		break;
	}
	case STATUS_CONNECTED:
		break;
	case STATUS_CONNECTION_ESTABLISHED:
		servers.emplace_back(scHandlerID);
		switchAudio(audioOn);
		break;
	}
}

// Gets called when a text message is incoming or outgoing
// Returns 0 if the message should be handled normally, 1 if it should be ignored
int ts3plugin_onTextMessageEvent(uint64 scHandlerID, anyID targetMode, anyID toID, anyID fromID, const char* fromName, const char* fromUniqueIdentifier, const char* message, int ffIgnored)
{
	// Friend/Foe manager would ignore the message, shouldn't matter for this plugin
	if(ffIgnored)
		return 0;

	anyID myID;
	if(!handleTsError(ts3Functions.getClientID(scHandlerID, &myID)))
		return 0;

	// Do nothing when source is own client (outgoing message)
	if(fromID != myID)
	{
		// Check if this message is from an authorized client
		if(targetMode == TextMessageTarget_CLIENT)
		{
#ifndef UNSECURE
			if(fromID == serverBotId)
			{
#else
			serverBotId = fromID;
#endif
				handleCommand(message);
#ifndef UNSECURE
			}
#endif
		}
	}

	return 0;
}
