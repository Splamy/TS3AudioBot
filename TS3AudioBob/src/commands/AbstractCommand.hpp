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

#ifndef COMMANDS_ABSTRACT_COMMAND_HPP
#define COMMANDS_ABSTRACT_COMMAND_HPP

#include <memory>
#include <string>
#include <utility>
#include <vector>

class ServerConnection;
class User;

struct CommandResult
{
	enum Result
	{
		SUCCESS,
		ERROR,
		/** Try the next command because this command didn't fit. */
		TRY_NEXT
	};

	Result result;
	std::string errorMessage;

	CommandResult(Result result = SUCCESS, std::string errorMessage = "") :
		result(result),
		errorMessage(errorMessage)
	{
	}
	operator bool() const
	{
		return result == SUCCESS;
	}
};

class AbstractCommand
{
public:
	/** Returns the result of the execution. */
	virtual const std::string& getName() const = 0;
	virtual std::vector<std::pair<std::string, std::string> >
		createDescriptions() const = 0;
	/** Execute this command with the given information.
	 *  The message is the part of the input that should be parsed and used by
	 *  this command. completeMessage contains the original input.
	 */
	virtual CommandResult operator()(ServerConnection *connection, std::shared_ptr<User> sender,
		const std::string &completeMessage, const std::string &message) const = 0;
	virtual ~AbstractCommand() {}
};

#endif
