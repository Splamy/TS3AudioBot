#include "Plugin.hpp"

#include <public_errors.h>
#include <ts3_functions.h>

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

static struct TS3Functions ts3Functions;
// Activate this to allow control messages from everyone and not only from the
// server query user (his ID is read from a file)
//#define UNSECURE

// Methods declarations

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
#ifndef UNSECURE
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
#endif

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
