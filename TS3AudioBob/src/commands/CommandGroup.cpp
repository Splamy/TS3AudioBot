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

#include "CommandGroup.hpp"

#include "Utils.hpp"

CommandGroup::CommandGroup(const std::string &name) :
	name(name)
{
}

CommandGroup::CommandGroup(CommandGroup &&group) :
	name(std::move(group.name)),
	subCommands(std::move(group.subCommands))
{
}

std::string CommandGroup::composeCommands(const std::vector<std::string> commands)
{
	// Insert current content of mergable into resulting descriptions
	std::ostringstream out;
	if (!commands.empty())
	{
		if (commands.size() == 1)
			out << ' ' << commands[0];
		else
		{
			out << " [";
			for (const std::string &s : commands)
				out << s << '|';
		}
	}
	std::string composed = out.str();
	if (commands.size() > 1)
		composed.back() = ']';
	return composed;
}

const std::string& CommandGroup::getName() const
{
	return name;
}

std::vector<std::pair<std::string, std::string> >
	CommandGroup::createDescriptions() const
{
	std::vector<std::pair<std::string, std::string> > result;
	// Variables to store commands that will be merged
	std::vector<std::string> mergable;
	std::string description;
	std::string lastCommandName;
	bool hasCommand = false;
	for (const auto &cmd : subCommands)
	{
		const std::string &commandName = cmd->getName();
		for (const auto &desc : cmd->createDescriptions())
		{
			if (commandName == lastCommandName && desc.second.empty() && hasCommand)
				// Insert command into mergable list
				mergable.push_back(desc.first);
			else
			{
				if (hasCommand)
					result.emplace_back(lastCommandName +
						composeCommands(mergable), description);

				// Insert new command
				mergable.clear();
				if (!desc.first.empty())
					mergable.push_back(desc.first);
				description = desc.second;
				lastCommandName = commandName;
				hasCommand = true;
			}
		}
	}
	// Insert last command if one exists
	if (hasCommand)
		result.emplace_back(lastCommandName + composeCommands(mergable),
			description);
	return result;
}

CommandResult CommandGroup::operator()(ServerConnection *connection,
	std::shared_ptr<User> sender, const std::string &completeMessage, const std::string &message) const
{
	if (subCommands.empty())
		// Useless command group
		return CommandResult(CommandResult::TRY_NEXT);

#ifdef COMMAND_DEBUG
	std::cout << Utils::format("Entering {0}\n", name);
#endif
	std::vector<std::string> commands;
	for (const std::unique_ptr<AbstractCommand> &sub : subCommands)
	{
		const std::string &name = sub->getName();
		if (std::find(commands.cbegin(), commands.cend(), name) == commands.cend())
			commands.emplace_back(name, 0);
	}

	std::string arguments = message;
	commands = CommandSystem::chooseWord(commands, arguments);

	if (commands.empty())
		return CommandResult::TRY_NEXT;

	if (commands.size() > 1)
	{
		// Can't get a distinct command string so give a useful list of possibly
		// meant commands
		std::ostringstream out;
		out << "error ambigious command\nMaybe you meant one of ";
		for (const auto &c : commands)
			out << "\n" << c;
		return CommandResult(CommandResult::TRY_NEXT, out.str());
	}

	// Execute command
	std::string commandName = commands[0];
	CommandResult result;
	for (const std::unique_ptr<AbstractCommand> &sub : subCommands)
	{
		if (sub->getName() == commandName)
		{
			result = (*sub)(connection, sender, completeMessage, arguments);
			if (result.result != CommandResult::TRY_NEXT)
				break;
		}
	}

#ifdef COMMAND_DEBUG
	std::cout << Utils::format("Leaving {0}\n", name);
#endif
	return result;
}
