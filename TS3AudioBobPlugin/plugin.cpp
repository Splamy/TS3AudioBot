#include <algorithm>
#include <cctype>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <functional>
#include <memory>
#include <sstream>
#include <string>
#include <vector>

#include "public_errors.h"
#include "ts3_functions.h"
#include "plugin.hpp"

static struct TS3Functions ts3Functions;
#define PATH_SIZE 1024
#ifdef _WIN32
#	if __cplusplus < 201103L
#		define override
#	endif
#else
#	if defined __GNUC__ && (__GNUC__ <= 4 || (__GNUC__ == 4 && __GNUC_MINOR__ < 7))
#		define override
#	endif
#endif
// Activate this to allow control messages from everyone and not only from the
// server query user (his ID is read from a file)
//#define UNSECURE

// Define things that are used to bind parameters to a variadic template function
/** A sequence of integers */
template<int...>
struct IntSequence{};
/** Used to create a sequence of integers recursively from 0 - (I - 1)
    (including I and appending Is if they exist) */
template<int I, int... Is>
struct IntSequenceCreator : IntSequenceCreator<I - 1, I - 1, Is...> {};
/** Final template instanciation of the recursion
    (break the recursion when 0 is reached) */
template<int... Is>
struct IntSequenceCreator<0, Is...> : IntSequence<Is...>{};
/** A placeholder that holds an int.
    It should be used with the IntSequenceCreator to generate a sequence of placeholders */
template<int>
struct Placeholder{};

// Define it as placeholder for the standard library so we can still bind the
// left over parameters
namespace std
{
	// The index of the placeholder will be its stored integer
	// Increment the indices because the placeholder expects 1 for the first placeholder
	template<int I>
	struct is_placeholder<Placeholder<I> > : integral_constant<int, I + 1>{};
}


// Methods declarations
template<class... Args>
static void log(const std::string &format, Args... args);
template<class... Args>
static void sendCommand(uint64 handlerID, const std::string &message, Args... args);
template<class... Args>
static void sendCommandAll(const std::string &message, Args... args);
static bool isSpace(char c);
static std::string strip(const std::string &input, bool left = true, bool right = true);
template<class R, class... Args, class P, class P2, int... Is>
static std::function<R(Args...)> myBind(const std::function<R(P, Args...)> &fun, P2 p, IntSequence<Is...>);

// Type declarations
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
	virtual bool operator()(uint64 sender, const std::string &message) = 0;
	virtual std::string getHelp() = 0;
};

template<class... Args>
class CommandExecutor : public AbstractCommandExecutor
{
public:
	typedef std::function<bool(uint64 sender, const std::string &message, Args...)> FuncType;

private:
	/** The function that should be invoked by this command. */
	FuncType fun;
	/** True, if this command should ignore arguments that can't be passed to the method. */
	bool ignoreMore;

public:
	CommandExecutor(FuncType f, bool ignore = false) :
		fun(f),
		ignoreMore(ignore)
	{
	}

private:
	// Calls fun by adding parameters recursively
	/** The function that is the last layer of the recursion. */
	template<int... Is>
	bool execute(uint64 sender, std::string message, std::function<bool()> f, IntSequence<Is...>)
	{
		if(!message.empty() && !ignoreMore)
		{
			log("Too many parameters");
			sendCommand(sender, "error too many parameters: '%s'", message.c_str());
			return false;
		}
		return f();
	}

	template<class P, class... Params, int... Is>
	bool execute(uint64 sender, std::string message, std::function<bool(P p, Params... params)> f, IntSequence<Is...>)
	{
		const std::string msg = strip(message, true, false);
		P p;
		std::istringstream input(msg);
		// Read booleans as true/false
		input >> std::boolalpha >> p;
		// Bind this parameter
		std::function<bool(Params...)> f2 = myBind(f, p, IntSequenceCreator<sizeof...(Params)>());
		int pos;
		// Test if it was successful
		if(input.eof())
			pos = -1;
		else if(!input)
			return false;
		else
			pos = input.tellg();
		// Drop the already read part of the message
		return input && execute(sender, pos == -1 ? "" : msg.substr(pos), f2, IntSequenceCreator<sizeof...(Params)>());
	}

public:
	bool operator()(uint64 sender, const std::string &message) override
	{
		// Bind sender
		std::function<bool(const std::string&, Args...)> f1 = myBind(fun, sender, IntSequenceCreator<sizeof...(Args) + 1>());
		// Bind message
		std::function<bool(Args...)> f = myBind(f1, message, IntSequenceCreator<sizeof...(Args)>());
		return execute(sender, message, f, IntSequenceCreator<sizeof...(Args)>());
	}
};

template<class... Args>
class StringCommandExecutor : public CommandExecutor<Args...>
{
public:
	typedef typename CommandExecutor<Args...>::FuncType FuncType;

private:
	/** The string that identities this command in lowercase. */
	const std::string command;
	const std::string help;

public:
	StringCommandExecutor(const std::string &command, const std::string &help, FuncType f, bool ignore = false) :
		CommandExecutor<Args...>(f, ignore),
		command(command),
		help(help)
	{
	}

	bool operator()(uint64 sender, const std::string &message) override
	{
		const std::string msg = strip(message);
		auto pos = std::find_if(msg.begin(), msg.end(), isSpace);
		std::string cmd = strip(pos == msg.end() ? msg : std::string(msg.begin(), pos));
		const std::string args = pos == msg.end() ? "" : strip(std::string(pos, msg.end()));
		std::transform(cmd.begin(), cmd.end(), cmd.begin(), ::tolower);
		return cmd == command && CommandExecutor<Args...>::operator()(sender, args);
	}

	std::string getHelp() override
	{
		return help;
	}
};

typedef std::vector<std::unique_ptr<AbstractCommandExecutor> > Commands;

// Attributes
static const std::string FILENAME = "queryId";
static const std::vector<std::string> quitMessages = { "I'm outta here", "You're boring", "Have a nice day", "Bye" };
static std::vector<uint64> whisperChannels;
static std::vector<anyID> whisperUsers;
static bool audioOn = false;
static std::vector<Server> servers;
static std::vector<BotAdmin> admins;
static Commands commands;
// TODO configure for multiple identities and use groups to get the rights
static anyID serverBotID = 0;

// Method implementations
/** Replaces occurences of a string in-place. */
static std::string& replace(std::string &input, const std::string &target, const std::string &replacement)
{
	std::size_t pos;
	while((pos = input.find(target)) != std::string::npos)
		input.replace(pos, target.size(), replacement);
	return input;
}

/** Returns a string with all whitespaces stripped at the beginning and the end. */
static std::string strip(const std::string &input, bool left, bool right)
{
	std::string::const_iterator start = input.begin();
	std::string::const_iterator end = input.end();
	if(left)
	{
		while(start <= end && std::isspace(*start))
			start++;
	}
	if(right)
		while(end > start && std::isspace(*--end));
	if(start == end)
		return "";
	return std::string(start, end + 1);
}

static bool isSpace(char c)
{
	return std::isspace(c);
}

template<class R, class... Args, class P, class P2, int... Is>
static std::function<R(Args...)> myBind(const std::function<R(P, Args...)> &fun, P2 p, IntSequence<Is...>)
{
	return std::bind(fun, p, Placeholder<Is>()...);
}

// Only print ascii chars and no control characters (maybe there can be problems
// with Remote Code Execution, that has to be verified)
/*static std::string onlyAscii(const std::string &input)
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
}*/

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
			// Broadcast error to all clients
			sendCommandAll("error %s", msg.c_str());
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
	//std::uniform_int_distribution<int> uniform(min, max);
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

static void refreshSending()
{
	setAudio(audioOn);
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
static void sendCommand(uint64 handlerID, uint64 userID, const std::string &message, Args... args)
{
	if (serverBotID == 0)
		log("The serverbot id is unknown :( Tried to write the following command: " + message, args...);
	else
	{
		// Create string
		std::vector<char> buf(1 + std::snprintf(NULL, 0, message.c_str(), args...));
		std::snprintf(buf.data(), buf.size(), message.c_str(), args...);
		handleTsError(ts3Functions.requestSendPrivateTextMsg(handlerID, buf.data(), userID, NULL));
	}
}

template<class... Args>
static void sendCommand(uint64 userID, const std::string &message, Args... args)
{
	for(std::vector<Server>::const_iterator it = servers.cbegin(); it != servers.cend(); it++)
		sendCommand(it->handlerID, userID, message, args...);
}

template<class... Args>
static void sendCommandAll(const std::string &message, Args... args)
{
	for(std::vector<Server>::const_iterator it = servers.cbegin(); it != servers.cend(); it++)
		sendCommand(it->handlerID, serverBotID, message, args...);
}

static void unknownCommand(const std::string& message)
{
	log("Unknown command: %s", message.c_str());
	std::string msg = message;
	replace(msg, "\n", "\\n");
	// Broadcast error to all clients
	sendCommandAll("error unknown command %s", msg.c_str());
}

// Commands
static bool helpCommand(uint64 sender, const std::string& /*message*/)
{
	sendCommand(sender, "help \n"
		"\taudio   [on|off]\n"
		"\tquality [on|off]\n"
		"\twhisper [on|off]\n"
		"\twhisper [add|remove] client <clientID>\n"
		"\twhisper [add|remove] channel <channelID>\n"
		"\twhisper clear\n"
		"\tstatus  audio\n"
		"\tstatus  whisper"
	);
	return true;
}

static bool pingCommand(uint64 sender, const std::string& /*message*/)
{
	sendCommand(sender, "pong");
	return true;
}

static bool exitCommand(uint64 /*sender*/, const std::string& /*message*/)
{
	closeBob();
	return true;
}

static bool audioCommand(uint64 /*sender*/, const std::string& /*message*/, bool on)
{
	setAudio(on);
	return true;
}

static bool loopCommand(uint64 /*sender*/, const std::string& message)
{
	std::string msg = message;
	std::transform(msg.begin(), msg.end(), msg.begin(), ::tolower);
	if(startsWith(msg, "error unknown") || startsWith(msg, "unknown command"))
	{
		log("Loop detected, have fun");
		return true;
	} else
		return false;
}

// TODO sense
static bool statusCommand(uint64 sender, const std::string& message, std::string a, int i)
{
	sendCommand(sender, "It works \\o/ : %s, %d", a.c_str(), i);
	return true;
}

static void handleCommand(uint64 sender, const std::string &message)
{
	//log("Handling message %s", message.c_str());
	bool found = false;
	for(Commands::const_iterator it = commands.cbegin(); it != commands.cend(); it++)
	{
		if((**it)(sender, message))
		{
			found = true;
			break;
		}
	}
	if(!found)
		unknownCommand(message);

	/*if(cmd == "audio on")
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
#define ADD_STRING_COMMAND(name, function, help) commands.push_back(\
	std::unique_ptr<AbstractCommandExecutor>(new StringCommandExecutor<>(name, help, function)))
	// Add simple commands
	ADD_STRING_COMMAND("help",	helpCommand, "Gives you this handy command list");
	ADD_STRING_COMMAND("ping",	pingCommand, "Returns with a pong if the Bob is alive");
	ADD_STRING_COMMAND("exit",	exitCommand, "Let the Bob go home");
#undef ADD_STRING_COMMAND
#define ADD_STRING_COMMAND(name, function, args, help) commands.push_back(\
	std::unique_ptr<AbstractCommandExecutor>(new StringCommandExecutor<args>(name, help, function)))
	// Add commands with arguments
	ADD_STRING_COMMAND("audio",   audioCommand, bool, "");
	commands.push_back(std::unique_ptr<AbstractCommandExecutor>(new StringCommandExecutor<std::string, int>("status", "", statusCommand, true)));
#undef ADD_STRING_COMMAND
#define ADD_STRING_COMMAND(name, function, help) commands.push_back(\
	std::unique_ptr<AbstractCommandExecutor>(new StringCommandExecutor<>(name, help, function, true)))
	// Ignore more arguments for the following commands
	ADD_STRING_COMMAND("error",   loopCommand, "");
	ADD_STRING_COMMAND("unknown", loopCommand, "");
#undef ADD_STRING_COMMAND

	/*commands.emplace("help", helpCommand);
	commands.emplace("ping", pingCommand);
	commands.emplace("exit", exitCommand);*/
	//commands.emplace("audio", []() { sendCommand("pong"); });
	//commands.emplace("quality", []() { sendCommand("pong"); });
	//commands.emplace("whisper", []() { sendCommand("pong"); });
	//commands.emplace("status", []() { sendCommand("pong"); });
	/*std::function<void()> loopCommand = []()
	{
		// TODO test for command
		log("Loop detected, have fun");
	};*/
	/*commands.emplace("Unknown", loopCommand);
	commands.emplace("unknown", loopCommand);
	commands.emplace("error", loopCommand);*/

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
void ts3plugin_onConnectStatusChangeEvent(uint64 scHandlerID, int newStatus, unsigned int /*errorNumber*/)
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
int ts3plugin_onTextMessageEvent(uint64 scHandlerID, anyID targetMode, anyID /*toID*/, anyID fromID, const char* /*fromName*/, const char* /*fromUniqueIdentifier*/, const char* message, int ffIgnored)
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
				handleCommand(fromID, message);
#ifndef UNSECURE
			}
#endif
		}
	}

	return 0;
}
