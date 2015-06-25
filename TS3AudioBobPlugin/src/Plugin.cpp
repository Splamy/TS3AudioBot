#include "Plugin.hpp"

#include <ServerBob.hpp>
#include <ServerConnection.hpp>

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

static TS3Functions ts3Functions;

static const char *VERSION = "2.0";

// Activate this to allow control messages from everyone and not only from the
// server query user (his ID is read from a file)
//#define UNSECURE

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
	serverBob.reset(new ServerBob(ts3Functions));
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
		serverBob->removeServer(scHandlerID);
		break;
	case STATUS_CONNECTED:
		break;
	case STATUS_CONNECTION_ESTABLISHED:
		serverBob->addServer(scHandlerID);
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
	if(!serverBob->handleTsError(serverBob->functions.getClientID(scHandlerID, &myID)))
		return 0;

	// Do nothing when source is own client (outgoing message)
	if(fromID != myID)
	{
		if(targetMode == TextMessageTarget_CLIENT)
		{
			std::string msg(message);
			serverBob->handleCommand(scHandlerID, fromID, msg);
		}
	}

	return 0;
}
