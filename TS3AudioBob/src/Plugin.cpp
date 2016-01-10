#include "Plugin.hpp"

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

#include <ServerBob.hpp>
#include <ServerConnection.hpp>
#include <TsApi.hpp>

namespace
{
static const char *VERSION = "2.2";
static const std::string CONFIG_FILE = "../configTS3AudioBot.cfg";
static const std::string ADMIN_Id_CONFIG_STRING = "MainBot::adminGroupId=";
static const std::size_t PATH_SIZE = 1024;

static std::unique_ptr<ServerBob> serverBob;
static std::shared_ptr<TsApi> tsApi;
}

// TeamSpeak library functions
// Required functions

// Unique name of this plugin
const char* ts3plugin_name()
{
	return "TS3AudioBob";
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
	return "Lets the TS3AudioBot control the TS3AudioBob.";
}

// Set the callback function pointers
void ts3plugin_setFunctionPointers(const struct TS3Functions funcs)
{
	tsApi.reset(new TsApi(funcs));
}

// Initialize the plugin
// Return 0 on success or 1 if an error occurs
int ts3plugin_init()
{
	// App and Resources path are empty for a console client
	// We take all available paths with a priority
	std::array<char[PATH_SIZE], 4> paths;
	tsApi->getFunctions().getPluginPath(paths[0], PATH_SIZE);
	tsApi->getFunctions().getConfigPath(paths[1], PATH_SIZE);
	tsApi->getFunctions().getAppPath(paths[2], PATH_SIZE);
	tsApi->getFunctions().getResourcesPath(paths[3], PATH_SIZE);

	// Get the server query id from a file
	for (const char (&path)[PATH_SIZE] : paths)
	{
		std::string file = std::string(path) + CONFIG_FILE;
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
						tsApi->log("Couldn't parse admin group id");
					else
						serverBob.reset(new ServerBob(tsApi, id));
					break;
				}
			}
			if (!serverBob)
				tsApi->log("Couldn't find admin group id field");
			break;
		} else
		{
			tsApi->log("Couldn't find config file at {0}", file);
		}
	}
	if (!serverBob)
	{
		tsApi->log("Couldn't read config file");
		// We don't want an uncontrollable Bob
		tsApi.reset();
		exit(EXIT_FAILURE);
	}
	return 0;
}

// Unload the plugin
void ts3plugin_shutdown()
{
	tsApi.reset();
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
	if (!tsApi->handleTsError(tsApi->getFunctions().getClientID(scHandlerId,
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

void ts3plugin_onEditCapturedVoiceDataEvent(uint64 scHandlerId,
	short *samples, int sampleCount, int channels, int *edited)
{
	if (serverBob && serverBob->fillAudioData(scHandlerId,
		reinterpret_cast<uint8_t*>(samples),
		sampleCount * channels * sizeof(short), channels, *edited & 2))
		*edited |= 1;
}
