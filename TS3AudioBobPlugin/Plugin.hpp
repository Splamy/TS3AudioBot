#ifndef PLUGIN_HPP
#define PLUGIN_HPP

#include <Definitions.hpp>
#include <public_definitions.h>

#include <cstdlib>

#ifdef __cplusplus
extern "C" {
#endif

// Required functions that are needed for the plugin to load
DLL_PUBLIC const char* ts3plugin_name();
DLL_PUBLIC const char* ts3plugin_version();
DLL_PUBLIC int ts3plugin_apiVersion();
DLL_PUBLIC const char* ts3plugin_author();
DLL_PUBLIC const char* ts3plugin_description();
DLL_PUBLIC void ts3plugin_setFunctionPointers(const struct TS3Functions funcs);
DLL_PUBLIC int ts3plugin_init();
DLL_PUBLIC void ts3plugin_shutdown();

// Optional fucntions
DLL_PUBLIC int ts3plugin_requestAutoload();

// Callbacks
DLL_PUBLIC void ts3plugin_onConnectStatusChangeEvent(uint64 serverConnectionHandlerID,
	int newStatus, unsigned int errorNumber);
DLL_PUBLIC int  ts3plugin_onTextMessageEvent(uint64 serverConnectionHandlerID,
	anyID targetMode, anyID toID, anyID fromID, const char* fromName,
	const char* fromUniqueIdentifier, const char* message, int ffIgnored);

#ifdef __cplusplus
}
#endif

#endif
