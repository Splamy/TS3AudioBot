#include "Plugin.hpp"

#include <ServerBob.hpp>
#include <ServerConnection.hpp>

#include <public_errors.h>
#include <ts3_functions.h>

#include <algorithm>
#include <array>
#include <cctype>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <functional>
#include <memory>
#include <sstream>
#include <string>
#include <vector>

static TS3Functions ts3Functions;

static const char *VERSION = "2.0";
static const std::string CONFIG_FILE = "../Bot/configTS3AudioBot.cfg";
static const std::string ADMIN_Id_CONFIG_STRING = "MainBot::adminGroupId=";
static const std::size_t PATH_SIZE = 1024;

static std::unique_ptr<ServerBob> serverBob;

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
	return VERSION;
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
	// App and Resources path are empty for a console client
	// We take all available paths with a priority
	std::array<char[PATH_SIZE], 4> paths;
	ts3Functions.getPluginPath(paths[0], PATH_SIZE);
	ts3Functions.getConfigPath(paths[1], PATH_SIZE);
	ts3Functions.getAppPath(paths[2], PATH_SIZE);
	ts3Functions.getResourcesPath(paths[3], PATH_SIZE);

	// Get the server query id from a file
	for (std::array<char[PATH_SIZE], 4>::const_iterator it = paths.cbegin();
	     it != paths.cend(); it++)
	{
		std::string file = std::string(*it) + CONFIG_FILE;
		std::ifstream configFile(file);
		if (configFile)
		{
			// Read the config file to get the admin group id
			std::string line;
			while (std::getline(configFile, line))
			{
				if (!Utils::startsWith(line, ";") &&
				    !Utils::startsWith(line, "//") &&
				    !Utils::startsWith(line, "#") &&
				    Utils::startsWith(line, ADMIN_Id_CONFIG_STRING))
				{
					std::istringstream parse(
						line.substr(ADMIN_Id_CONFIG_STRING.size()));
					anyID id;
					parse >> id;
					if (!parse)
						ts3Functions.logMessage("Couldn't parse admin group id",
							LogLevel_ERROR, "", 0);
					else
						serverBob.reset(new ServerBob(ts3Functions, id));
					break;
				}
			}
			if (!serverBob)
				ts3Functions.logMessage("Couldn't find admin group id field",
					LogLevel_ERROR, "", 0);
			break;
		}
	}
	if (!serverBob)
	{
		ts3Functions.logMessage("Couldn't read config file", LogLevel_ERROR, "", 0);
		// We don't want an uncontrollable Bob
		exit(1);
	}
	return 0;
}

// Unload the plugin
void ts3plugin_shutdown()
{
	serverBob.reset();
}

// Optional functions
// Returns 1 if the plugin should be autoloaded, 0 otherwise
int ts3plugin_requestAutoload()
{
	return 1;
}

// Callbacks executed by the TeamSpeak when an event occurs
void ts3plugin_onConnectStatusChangeEvent(uint64 scHandlerId, int newStatus,
	unsigned int /*errorNumber*/)
{
	switch (newStatus)
	{
	case STATUS_DISCONNECTED:
		serverBob->removeServer(scHandlerId);
		break;
	case STATUS_CONNECTED:
		serverBob->addServer(scHandlerId);
		break;
	case STATUS_CONNECTION_ESTABLISHED:
		break;
	}
}

// Gets called when a text message is incoming or outgoing
// Returns 0 if the message should be handled normally, 1 if it should be
// ignored
int ts3plugin_onTextMessageEvent(uint64 scHandlerId, anyID targetMode,
	anyID /*toId*/, anyID fromId, const char * /*fromName*/,
	const char *fromUniqueIdentifier, const char *message, int ffIgnored)
{
	// Friend/Foe manager would ignore the message, shouldn't matter for this
	// plugin
	if (ffIgnored)
		return 0;

	anyID myId;
	if (!serverBob->handleTsError(serverBob->functions.getClientID(scHandlerId,
	    &myId)))
		return 0;

	// Do nothing when source is own client (outgoing message)
	if (fromId != myId && targetMode == TextMessageTarget_CLIENT)
	{
		std::string msg(message);
		serverBob->handleCommand(scHandlerId, fromId, fromUniqueIdentifier, msg);
	}

	return 0;
}

void ts3plugin_onClientDBIDfromUIDEvent(uint64 scHandlerId,
	const char *uniqueClientIdentifier, uint64 clientDatabaseId)
{
	serverBob->gotDbId(scHandlerId, uniqueClientIdentifier, clientDatabaseId);
}

void ts3plugin_onServerGroupByClientIDEvent(uint64 scHandlerId,
	const char * /*name*/, uint64 serverGroup, uint64 clientDatabaseId)
{
	serverBob->gotServerGroup(scHandlerId, clientDatabaseId, serverGroup);
}
