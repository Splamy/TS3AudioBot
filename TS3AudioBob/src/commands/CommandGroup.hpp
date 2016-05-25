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

#ifndef COMMANDS_COMMAND_GROUP_HPP
#define COMMANDS_COMMAND_GROUP_HPP

#include "Command.hpp"

class CommandGroup : public AbstractCommand
{
private:
	std::string name;
	std::vector<std::unique_ptr<AbstractCommand> > subCommands;

	static std::string composeCommands(const std::vector<std::string> commands);

public:
	CommandGroup(const std::string &name);
	CommandGroup(CommandGroup&) = delete;
	CommandGroup(CommandGroup &&group);
	virtual ~CommandGroup() {}

	const std::string& getName() const override;

	std::vector<std::pair<std::string, std::string> >
		createDescriptions() const override;
	CommandResult operator()(ServerConnection *connection, std::shared_ptr<User> sender,
		const std::string &completeMessage, const std::string &message) const override;

	template <class... Args>
	void addCommand(const std::string &command, std::function<CommandResult
		(ServerConnection*, std::shared_ptr<User>, const std::string&, const std::string&, Args...)> fun,
		const std::string &description = "", bool displayDescription = true)
	{
		std::string::size_type commandEnd = command.find(' ');
		std::string commandName = command.substr(0, commandEnd);
		std::string rest = commandEnd == std::string::npos ? "" : command.substr(
			commandEnd + 1);
		// There can be errors if (commandEnd + 1) is out of range
		// They will only occur if a command name string is malformated
		// (has one trailing space) so be sure to not mess up the command names.
		if (commandEnd == std::string::npos || command[commandEnd + 1] == '<' ||
			command[commandEnd + 1] == '[')
		{
			// Command end point
			subCommands.emplace_back(new Command<Args...>(commandName, rest,
				fun, description, displayDescription));
		} else
		{
			// New command group
			// Search for existing command group with the same name
			for (std::unique_ptr<AbstractCommand> &sub : subCommands)
			{
				CommandGroup *cmdGroup = dynamic_cast<CommandGroup*>(sub.get());
				if (cmdGroup && sub->getName() == commandName)
				{
					cmdGroup->addCommand(rest, fun, description,
						displayDescription);
					return;
				}
			}
			// Name not found -> insert a new command group
			std::unique_ptr<AbstractCommand> newCmd(new CommandGroup(commandName));
			CommandGroup *group = static_cast<CommandGroup*>(newCmd.get());
			group->addCommand(rest, fun, description, displayDescription);
			subCommands.push_back(std::move(newCmd));
		}
	}
};

#endif
