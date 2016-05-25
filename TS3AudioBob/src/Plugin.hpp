// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

#ifndef PLUGIN_HPP
#define PLUGIN_HPP

#include "Definitions.hpp"
#include "public_definitions.h"
#include <cstdlib>

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
