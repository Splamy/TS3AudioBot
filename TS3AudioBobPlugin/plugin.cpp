#include <algorithm>
#include <cctype>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <functional>
#include <map>
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

struct BotAdmin
{
	anyID clientID;
};

class AbstractCommandExecutor
{
public:
	// Returns if the command was handled
	virtual bool operator() (uint64 sender, const std::string &message) = 0;
};

template<class... Args>
class CommandExecutor : public AbstractCommandExecutor
{
private:
	std::function<bool(uint64 sender, const std::string &message, Args...)> fun;

public:
	// Calls fun by adding parameters recursively
	bool execute(std::string message, std::function<bool()> f)
	{
		return f();
	}

	template<class P, class... Params>
	bool execute(std::string message, std::function<bool(P p, Params... params)> f)
	{
		return execute(std::bind(NULL, f));
	}

	bool operator() (uint64 sender, const std::string &message)
	{
		return execute(message, std::bind(sender, message, fun));
	}
};

// TODO pass some parameters: arguments, sender, command
typedef std::map<std::string, std::function<void(const std::string &command)> > CommandMap;

// Attributes
static const std::string FILENAME = "queryId";
static const std::vector<std::string> quitMessages = { "I'm outta here", "You're boring", "Have a nice day", "Bye" };
static std::vector<uint64> whisperChannels;
static std::vector<anyID> whisperUsers;
static bool audioOn = false;
static std::vector<Server> servers;
static std::vector<BotAdmin> admins;
static CommandMap commands;
// TODO configure for multiple identities
static anyID serverBotID = 0;

// Methods declarations
template<class... Args>
static void sendCommand(uint64 handlerID, const std::string &message, Args... args);
template<class... Args>
static void sendCommand(const std::string &message, Args... args);

// Method implementations
// Replaces occurences of a string in-place
static std::string& replace(std::string &input, const std::string &target, const std::string &replacement)
{
	std::size_t pos;
	while((pos = input.find(target)) != std::string::npos)
		input.replace(pos, target.size(), replacement);
	return input;
}

// Returns a string with all whitespaces stripped at the beginning and the end
static std::string strip(const std::string &input)
{
	std::string::const_iterator start = input.begin();
	std::string::const_iterator end = input.end();
	while(std::isspace(*start))
		start++;
	while(std::isspace(*end))
		end--;
	return std::move(std::string(start, end));
}


// Only print ascii chars and no control characters (apparently there can be problems
// with Remote Code Execution)
static std::string onlyAscii(const std::string &input)
{
	char *result = new char[input.size()];
	int j = 0;
	char c;
	for(int i = 0; (c = input[i]); i++)
	{
		// ' ' - '~'
		if(c >= 32 && c <= 126)
			result[j++] = c;
	}
	result[j] = '\0';
	std::string str = result;
	delete[] result;
	return std::move(str);
}

bool startsWith(const std::string &string, const std::string &prefix)
{
	return prefix.size() <= string.size() && std::equal(prefix.begin(), prefix.end(), string.begin());
}

template<class... Args>
static void log(const std::string &format, Args... args)
{
	printf(format.c_str(), args...);
	printf("\n");
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


// Bob library functions
static bool useWhispering()
{
	return !whisperChannels.empty() || !whisperUsers.empty();
}

static void setAudio(bool on)
{
	std::vector<uint64> targets(whisperChannels);
	targets.emplace_back(0);
	std::vector<anyID> targetClients(whisperUsers);
	targetClients.emplace_back(0);
	for(std::vector<Server>::const_iterator it = servers.cbegin(); it != servers.cend(); it++)
	{
		if(on)
		{
			if(useWhispering())
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

static void setQuality(bool on)
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
	setQuality(false);
	serverBotID = 0;
	std::string msg = quitMessages[getRandomNumber(0, quitMessages.size())];
	for(std::vector<Server>::const_iterator it = servers.cbegin(); it != servers.cend(); it++)
		handleTsError(ts3Functions.stopConnection(it->handlerID, msg.c_str()));
	// "Graceful" exit
	exit(0);
}

template<class... Args>
static void sendCommand(uint64 handlerID, const std::string &message, Args... args)
{
	if (serverBotID == 0)
		log("The serverbot id is unknown :( Tried to write the following command: " + message, args...);
	else
	{
		// Create string
		std::vector<char> buf(1 + std::snprintf(NULL, 0, message.c_str(), args...));
		std::snprintf(buf.data(), buf.size(), message.c_str(), args...);
		handleTsError(ts3Functions.requestSendPrivateTextMsg(handlerID, buf.data(), serverBotID, NULL));
	}
}

template<class... Args>
static void sendCommand(const std::string &message, Args... args)
{
	for(std::vector<Server>::const_iterator it = servers.cbegin(); it != servers.cend(); it++)
		sendCommand(it->handlerID, message, args...);
}

static void unknownCommand(const std::string &command)
{
	log("Unknown command: %s", command.c_str());
	sendCommand("error unknown command");
}

// Commands
static void helpCommand(const std::string &command)
{
	sendCommand("help \n"
		"\taudio   [on|off]\n"
		"\tquality [on|off]\n"
		"\twhisper [on|off]\n"
		"\twhisper [add|remove] client <clientID>\n"
		"\twhisper [add|remove] channel <channelID>\n"
		"\twhisper clear\n"
		"\tstatus  audio\n"
		"\tstatus  whisper"
	);
}

static void pingCommand(const std::string &command)
{
	sendCommand("pong");
}

static void exitCommand(const std::string &command)
{
	closeBob();
}

static void loopCommand(const std::string &message)
{
	std::string msg = message;
	std::transform(msg.begin(), msg.end(), msg.begin(), ::tolower);
	if(msg == "error unknown command" || msg == "unknown command")
		log("Loop detected, have fun");
}

static bool isSpace(char c)
{
	return std::isspace(c);
}
static void handleCommand(const std::string &message)
{
	// Extract command part
	std::string msg = strip(message);
	std::string::iterator pos = std::find_if(msg.begin(), msg.end(), isSpace);
	std::string command = strip(pos == msg.end() ? msg : std::string(msg.begin(), pos));
	std::transform(command.begin(), command.end(), command.begin(), ::tolower);
	//TODO Get arguments
	//std::string args = strip(pos == msg.end() ? "" : std::string(pos, msg.end()));
	//log("Handling message %s", message.c_str());

	CommandMap::iterator it = commands.find(command);
	if(it != commands.end())
	{
		CommandMap::mapped_type fun = it->second;
		fun(msg);
	} else
		unknownCommand(message);

	/*if(cmd == "exit")
		closeBob();
	else if(cmd == "help")
	{
		sendCommand("help \n"
			"\taudio [on|off]\n"
			"\tquality [on|off]\n"
			"\twhisper [on|off]\n"
			"\twhisper [add|remove] client <clientID>\n"
			"\twhisper [add|remove] channel <channelID>\n"
			"\twhisper clear\n"
			"\tstatus audio\n"
			"\tstatus whisper"
		);
	} else if(cmd == "audio on")
		setAudio(true);
	else if(cmd == "audio off")
		setAudio(false);
	else if(cmd == "quality on")
		setQuality(true);
	else if(cmd == "quality off")
		setQuality(false);
	else if(cmd == "whisper clear")
	{
		whisperChannels.clear();
		whisperUsers.clear();
		// Update send status
		setAudio(audioOn);
	} else if(startsWith(cmd, "whisper add channel"))
	{
		uint64 id;
		if(std::sscanf(cmd.c_str(), "whisper add channel %lu", &id) == 1)
		{
			whisperChannels.emplace_back(id);
			setAudio(audioOn);
		} else
			sendCommand("error parsing channel id");
	} else if(startsWith(cmd, "whisper add client"))
	{
		anyID id;
		if(std::sscanf(cmd.c_str(), "whisper add client %hu", &id) == 1)
		{
			whisperUsers.emplace_back(id);
			setAudio(audioOn);
		} else
			sendCommand("error parsing client id");
	} else if(startsWith(cmd, "whisper remove client"))
	{
		anyID id;
		if(std::sscanf(cmd.c_str(), "whisper remove client %hu", &id) == 1)
		{
			std::vector<anyID>::iterator it = std::find(whisperUsers.begin(), whisperUsers.end(), id);
			if(it != whisperUsers.end())
			{
				whisperUsers.erase(it);
				setAudio(audioOn);
			} else
				sendCommand("error finding client id");
		} else
			sendCommand("error parsing client id");
	} else if(startsWith(cmd, "whisper remove channel"))
	{
		uint64 id;
		if(std::sscanf(cmd.c_str(), "whisper remove channel %lu", &id) == 1)
		{
			std::vector<uint64>::iterator it = std::find(whisperChannels.begin(), whisperChannels.end(), id);
			if(it != whisperChannels.end())
			{
				whisperChannels.erase(it);
				setAudio(audioOn);
			} else
				sendCommand("error finding channel id");
		} else
			sendCommand("error parsing channel id");
	} else if(cmd == "status audio")
		sendCommand("status audio %s", audioOn ? "on" : "off");
	else if(cmd == "status whisper")
	{
		sendCommand("status whisper %s", useWhispering() ? "on" : "off");
		// Write clients and channels that are set in the whisperlist
		for(std::vector<uint64>::const_iterator it = whisperChannels.cbegin(); it != whisperChannels.cend(); it++)
			sendCommand("status whisper channel %lu", *it);
		for(std::vector<anyID>::const_iterator it = whisperUsers.cbegin(); it != whisperUsers.cend(); it++)
			sendCommand("status whisper client %hu", *it);
	} else if(cmd == "unknown command")
		log("Loop detected, have fun");
	else
	{
		log("Unknown command");
		sendCommand("error unknown command");
	}*/
}


// TeamSpeak library functions
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
	return "Seebi";
}

// Description of this plugin
const char* ts3plugin_description()
{
	return "Lets the TS3AudioBot control the TS3AudioBobPlugin.";
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
	// Register commands
	commands.emplace("help", helpCommand);
	commands.emplace("ping", pingCommand);
	commands.emplace("exit", exitCommand);
	//commands.emplace("audio", []() { sendCommand("pong"); });
	//commands.emplace("quality", []() { sendCommand("pong"); });
	//commands.emplace("whisper", []() { sendCommand("pong"); });
	//commands.emplace("status", []() { sendCommand("pong"); });
	/*std::function<void()> loopCommand = []()
	{
		// TODO test for command
		log("Loop detected, have fun");
	};*/
	commands.emplace("Unknown", loopCommand);
	commands.emplace("unknown", loopCommand);
	commands.emplace("error", loopCommand);

	// Get currently active connections
	uint64 *handlerIDs;
	if(!handleTsError(ts3Functions.getServerConnectionHandlerList(&handlerIDs)))
		return 1;
	for(uint64 *handlerID = handlerIDs; *handlerID != 0; handlerID++)
		servers.emplace_back(*handlerID);
	ts3Functions.freeMemory(handlerIDs);

	setAudio(audioOn);

	// App and Resources path are empty for a console client
	// We take all of them with a priority
	char paths[4][PATH_SIZE];
	ts3Functions.getAppPath(paths[0], PATH_SIZE);
	ts3Functions.getResourcesPath(paths[1], PATH_SIZE);
	ts3Functions.getPluginPath(paths[2], PATH_SIZE);
	ts3Functions.getConfigPath(paths[3], PATH_SIZE);

	// Get the server query id from a file
	for(std::size_t i = 0; i < 4; i++)
	{
		std::string file = std::string(paths[i]) + FILENAME;
		std::ifstream in(file);
		int id = 0;
		if(in)
			in >> id;

		if(id != 0 && in)
		{
			// Successfully read id
			serverBotID = id;
			break;
		}
	}
	if(serverBotID == 0)
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
		setAudio(audioOn);
		// Query connected clients
		// TODO Get database id
		//handleTsError(ts3Functions.requestClientDBIDfromUID(scHandlerID, fromUniqueIdentifier, NULL));
		// Get assigned server groups
		//handleTsError(ts3Functions.requestServerGroupsByClientID(scHandlerID, clientID, NULL));
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
			if(fromID == serverBotID)
			{
#else
			serverBotID = fromID;
#endif
				handleCommand(message);
#ifndef UNSECURE
			}
#endif
		}
	}

	return 0;
}
