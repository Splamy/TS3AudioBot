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

#ifndef TS_API_HPP
#define TS_API_HPP

#include "Utils.hpp"
#include <ts3_functions.h>
#include <iostream>

class TsApi
{
private:
	const TS3Functions functions;

public:
	TsApi(const TS3Functions &functions);

	const TS3Functions& getFunctions() const;
	/** This function simplifies the handling of TeamSpeak-API-call errors.
	 *  You can simply write the following code:
	 *  \code
	 *  	if (api->handleTsError(api->getFunctions().doSomething()))
	 *  	{
	 *  		// Call successful
	 *  	} else
	 *  	{
	 *  		// Call failed
	 *  	}
	 *  \endcode
	 *
	 *  @param error The return value of a TeamSpeak-API-call.
	 *  @return True, if the error signals a successful call, otherwise false.
	 */
	bool handleTsError(unsigned int error) const;

	/** Print a message to the TeamSpeak log or, if that's not possible, to
	 *  stdout. This function takes the same arguments as Utils::format.
	 *  If the message is printed to stdout, asscii control characters and 
	 *  newlines are removed before.
	 *
	 *  @see Utils::format
	 */
	template <class... Args>
	void log(const std::string &format, Args... args) const
	{
		std::string message = Utils::format(format, args...);
		Utils::sanitizeLines(message);
		if (!handleTsError(functions.logMessage(message.c_str(), LogLevel_INFO,
				"AudioBob", 0)))
		{
			// Print the message to stdout when logging with TeamSpeak failed
			// Remove every left control character for more security
			message = Utils::sanitizeAscii(message);
			std::cout << message;
		}
	}
};

#endif
