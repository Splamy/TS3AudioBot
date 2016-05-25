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

#include "TsApi.hpp"

#include <public_errors.h>

TsApi::TsApi(const TS3Functions &functions) :
	functions(functions)
{
}

const TS3Functions& TsApi::getFunctions() const
{
	return functions;
}

bool TsApi::handleTsError(unsigned int error) const
{
	if (error != ERROR_ok)
	{
		char* errorMsg;
		if (functions.getErrorMessage(error, &errorMsg) == ERROR_ok)
		{
			// Send the message to the bot
			std::string msg = errorMsg;
			functions.freeMemory(errorMsg);
			log("TeamSpeak-error: {0}", msg);
		} else
			log("TeamSpeak-double-error ({0})", error);
		return false;
	}
	return true;
}
