#ifndef PLUGIN_HPP
#define PLUGIN_HPP

#include <cstdlib>

#include <Definitions.hpp>
#include <public_definitions.h>

extern "C"
{
// Required functions that are needed for the plugin to load
DLL_PUBLIC const char* ts3plugin_name();
DLL_PUBLIC const char* ts3plugin_version();
DLL_PUBLIC int         ts3plugin_apiVersion();
DLL_PUBLIC const char* ts3plugin_author();
DLL_PUBLIC const char* ts3plugin_description();
DLL_PUBLIC void        ts3plugin_setFunctionPointers(const struct TS3Functions funcs);
DLL_PUBLIC int         ts3plugin_init();
DLL_PUBLIC void        ts3plugin_shutdown();

// Optional fucntions
DLL_PUBLIC int ts3plugin_requestAutoload();

// Callbacks
DLL_PUBLIC void ts3plugin_onConnectStatusChangeEvent(uint64 serverConnectionHandlerId,
	int newStatus, unsigned int errorNumber);
DLL_PUBLIC int  ts3plugin_onTextMessageEvent(uint64 serverConnectionHandlerId,
	anyID targetMode, anyID toId, anyID fromId, const char *fromName,
	const char *fromUniqueIdentifier, const char *message, int ffIgnored);
DLL_PUBLIC void ts3plugin_onClientDBIDfromUIDEvent(uint64 serverConnectionHandlerId,
	const char *uniqueClientIdentifier, uint64 clientDatabaseId);
DLL_PUBLIC void ts3plugin_onServerGroupByClientIDEvent(uint64 serverConnectionHandlerId,
	const char *name, uint64 serverGroupList, uint64 clientDatabaseId);
DLL_PUBLIC void ts3plugin_onEditCapturedVoiceDataEvent(uint64 scHandlerID,
	short *samples, int sampleCount, int channels, int *edited);
}

#endif
