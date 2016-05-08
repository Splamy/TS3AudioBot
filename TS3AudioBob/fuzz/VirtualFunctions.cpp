#include "Plugin.hpp"
#include "Utils.hpp"
#include "VirtualFunctions.hpp"

#include <public_definitions.h>

#include <iostream>
#include <cstring>

static const uint64 SERVER_ID = 1;
static const anyID OWN_ID = 2;
static const anyID OTHER_ID = 3;
static const uint64 OTHER_DBID = 4;
static const uint64 CHANNEL_ID = 6;
static const char *MY_CHANNEL_NAME = "channel_name";
static const char *OTHER_NAME = "client_name";
static const char *OTHER_UID = "own_uid";
static const char *OWN_NAME = "own_name";
static const char *OWN_UID = "own_uid";

std::queue<std::function<void()> > commandQueue;

unsigned int logMessage(const char *message, LogLevel severity, const char *channel, uint64 /*id*/)
{
	std::cout << Utils::format("Log: [{0}] {1} {2}\n", channel, severity, message);
	return 0;
}

unsigned int getServerConnectionHandlerList(uint64 **result)
{
	*result = static_cast<uint64*>(std::malloc(2 * sizeof(uint64)));
	if (*result == nullptr)
		return 1;
	(*result)[0] = SERVER_ID;
	(*result)[1] = 0;
	return 0;
}

unsigned int freeMemory(void *pointer)
{
	std::free(pointer);
	return 0;
}

unsigned int requestClientSetWhisperList(uint64 /*serverConnectionHandlerID*/, anyID /*clientID*/, const uint64 * /*targetChannelIDArray*/, const anyID * /*targetClientIDArray*/, const char * /*returnCode*/)
{
	return 0;
}

unsigned int setClientSelfVariableAsInt(uint64 /*serverConnectionHandlerID*/, size_t /*flag*/, int /*value*/)
{
	return 0;
}

unsigned int getClientID(uint64 /*serverConnectionHandlerID*/, anyID *result)
{
	*result = OWN_ID;
	return 0;
}

void getPluginPath(char *path, size_t maxLen)
{
	static const char *p = ".ts3client/";
	for (std::size_t i = 0; i <= strlen(p) && i < maxLen; i++)
		path[i] = p[i];
}

void getConfigPath(char *path, size_t maxLen)
{
	if (maxLen > 0)
		path[0] = '\0';
}

void getAppPath(char *path, size_t maxLen)
{
	if (maxLen > 0)
		path[0] = '\0';
}

void getResourcesPath(char *path, size_t maxLen)
{
	if (maxLen > 0)
		path[0] = '\0';
}

unsigned int requestClientDBIDfromUID(uint64 serverConnectionHandlerID, const char *clientUniqueIdentifier, const char * /*returnCode*/)
{
	commandQueue.push(std::bind(ts3plugin_onClientDBIDfromUIDEvent, serverConnectionHandlerID, clientUniqueIdentifier, OTHER_DBID));
	return 0;
}

unsigned int requestServerGroupsByClientID(uint64 serverConnectionHandlerID, uint64 clientDatabaseID, const char * /*returnCode*/)
{
	commandQueue.push(std::bind(ts3plugin_onServerGroupByClientIDEvent, serverConnectionHandlerID, OTHER_NAME, ADMIN_GROUP_ID, clientDatabaseID));
	return 0;
}

unsigned int requestSendPrivateTextMsg(uint64 /*serverConnectionHandlerID*/, const char *message, anyID /*targetClientID*/, const char * /*returnCode*/)
{
	std::cout << Utils::format("Sending message: {0}\n", message);
	return 0;
}

unsigned int stopConnection(uint64 /*serverConnectionHandlerID*/, const char * /*message*/)
{
	return 0;
}

unsigned int getChannelList(uint64 /*serverConnectionHandlerID*/, uint64** result)
{
	*result = static_cast<uint64*>(std::malloc(2 * sizeof(uint64)));
	if (*result == nullptr)
		return 1;
	(*result)[0] = CHANNEL_ID;
	(*result)[1] = 0;
	return 0;
}

unsigned int getChannelVariableAsString(uint64 /*serverConnectionHandlerID*/, uint64 /*channelID*/, size_t flag, char **result)
{
	if (flag == CHANNEL_NAME)
	{
		*result = static_cast<char*>(std::malloc(strlen(MY_CHANNEL_NAME) + 1));
		std::memcpy(*result, MY_CHANNEL_NAME, strlen(MY_CHANNEL_NAME) + 1);
		return 0;
	}
	std::cout << "UNSUPPORTED FUNCTION\n";
	return 1;
}

unsigned int getClientList(uint64 /*serverConnectionHandler*/, anyID **result)
{
	*result = static_cast<anyID*>(std::malloc(3 * sizeof(anyID)));
	(*result)[0] = OWN_ID;
	(*result)[1] = OTHER_ID;
	(*result)[2] = 0;
	return 0;
}

unsigned int getClientVariableAsString(uint64 /*serverConnectionHandlerID*/, anyID clientID, size_t flag, char **result)
{
	if (flag == CLIENT_NICKNAME)
	{
		if (clientID == OWN_ID)
		{
			*result = static_cast<char*>(std::malloc(strlen(OWN_NAME) + 1));
			std::memcpy(*result, OWN_NAME, strlen(OWN_NAME) + 1);
		}
		else if (clientID == OTHER_ID)
		{
			*result = static_cast<char*>(std::malloc(strlen(OTHER_NAME) + 1));
			std::memcpy(*result, OTHER_NAME, strlen(OTHER_NAME) + 1);
		}
		else
			return 1;
		return 0;
	}
	else if (flag == CLIENT_UNIQUE_IDENTIFIER)
	{
		if (clientID == OWN_ID)
		{
			*result = static_cast<char*>(std::malloc(strlen(OWN_UID) + 1));
			std::memcpy(*result, OWN_UID, strlen(OWN_UID) + 1);
		}
		else if (clientID == OTHER_ID)
		{
			*result = static_cast<char*>(std::malloc(strlen(OTHER_UID) + 1));
			std::memcpy(*result, OTHER_UID, strlen(OTHER_UID) + 1);
		}
		else
			return 1;
		return 0;
	}
	// Die here
	char *ptr = nullptr;
	*ptr = *ptr;
	return 1;
}

unsigned int getErrorMessage(unsigned int errorCode, char **result)
{
	static const char *ERROR_MESSAGE = "error_message";
	std::cout << Utils::format("Asked error message for {0}\n", errorCode);
	*result = static_cast<char*>(std::malloc(strlen(ERROR_MESSAGE) + 1));
	std::memcpy(*result, ERROR_MESSAGE, strlen(ERROR_MESSAGE) + 1);
	return 0;
}

void initTS3Plugin()
{
	TS3Functions funs;
	std::memset(&funs, 0, sizeof(funs));
	funs.logMessage = &logMessage;
	funs.getServerConnectionHandlerList = &getServerConnectionHandlerList;
	funs.freeMemory = &freeMemory;
	funs.requestClientSetWhisperList = &requestClientSetWhisperList;
	funs.setClientSelfVariableAsInt = &setClientSelfVariableAsInt;
	funs.getClientID = &getClientID;
	funs.getPluginPath = &getPluginPath;
	funs.getConfigPath = &getConfigPath;
	funs.getAppPath = &getAppPath;
	funs.getResourcesPath = &getResourcesPath;
	funs.requestClientDBIDfromUID = &requestClientDBIDfromUID;
	funs.requestServerGroupsByClientID = &requestServerGroupsByClientID;
	funs.requestSendPrivateTextMsg = &requestSendPrivateTextMsg;
	funs.stopConnection = &stopConnection;
	funs.getChannelList = &getChannelList;
	funs.getChannelVariableAsString = &getChannelVariableAsString;
	funs.getClientList = &getClientList;
	funs.getClientVariableAsString = &getClientVariableAsString;
	funs.getErrorMessage = &getErrorMessage;

	ts3plugin_setFunctionPointers(funs);
	ts3plugin_init();
}

void shutdownTS3Plugin()
{
	ts3plugin_shutdown();
}

void sendMessage(const char *message)
{
	// Send message from 1 to 2
	std::cout << Utils::format("Writing message: {0}\n", message);
	ts3plugin_onTextMessageEvent(SERVER_ID, TextMessageTarget_CLIENT, OWN_ID, OTHER_ID, nullptr, OWN_UID, message, false);
}

void workCommandQueue()
{
	while (!commandQueue.empty())
	{
		commandQueue.front()();
		commandQueue.pop();
	}
}
