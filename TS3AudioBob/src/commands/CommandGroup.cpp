#include "CommandGroup.hpp"

#include "Utils.hpp"

#include <cassert>

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
	User *sender, const std::string &message) const
{
	if (subCommands.empty())
		// Useless command group
		return CommandResult(CommandResult::TRY_NEXT);

#ifdef COMMAND_DEBUG
	std::cout << Utils::format("Entering {0}\n", name);
#endif
	std::vector<std::pair<std::string, std::string::size_type> > commands;
	for (const std::unique_ptr<AbstractCommand> &sub : subCommands)
	{
		const std::string &name = sub->getName();
		if (std::none_of(commands.cbegin(), commands.cend(), [&name]
			(decltype(commands)::const_reference s)
			{ return s.first == name; }))
			commands.emplace_back(name, 0);
	}

	// Search if we can find a right method
	std::string::size_type msgIndex = 0;
	while (msgIndex < message.length())
	{
		// Stop if the command is over
		if (Utils::isSpace(message[msgIndex]))
		{
			// Skip following whitespaces
			do
			{
				msgIndex++;
			} while (msgIndex < message.length() && Utils::isSpace(message[msgIndex]));
			break;
		}
		// Stop if only one command is left
		if (commands.size() == 1)
		{
			// Skip to end of command
			while (msgIndex < message.length() && !Utils::isSpace(message[msgIndex]))
				msgIndex++;
			continue;
		}

		// Backup current commands
		decltype(commands) oldCommands = commands;
		// Filter commands
		for (std::size_t i = 0; i < commands.size(); i++)
		{
			std::string::size_type newPos =
				commands[i].first.find(message[msgIndex], commands[i].second);
			if (newPos == std::string::npos)
			{
				commands.erase(commands.cbegin() + i);
				i--;
			} else
				commands[i].second = newPos + 1;
		}
		// Ignore this character if there are no results
		// ATTENTION: This can probably lead to funny behaviour ;)
		if (commands.empty())
			commands = std::move(oldCommands);

		msgIndex++;
	}

	// Take the command with the lowest index
	std::string::size_type minIndex = std::string::npos;
	for (const auto &c : commands)
	{
		if (c.second < minIndex)
			minIndex = c.second;
	}
	for (std::size_t i = 0; i < commands.size(); i++)
	{
		if (commands[i].second != minIndex)
		{
			commands.erase(commands.cbegin() + i);
			i--;
		}
	}

#ifdef COMMAND_DEBUG
	if (commands.size() > 1)
		std::cout << Utils::format("Leaving {0} because of too many commands\n", name);
	else
	{
		std::cout << Utils::format("Found the following commands ({0}):", commands.size());
		for (const auto &c : commands)
			std::cout << " " << c.first;
		std::cout << "\n";
	}
#endif

	if (commands.size() > 1)
		// Can't get a distinct command string
		return CommandResult(CommandResult::TRY_NEXT, "error ambigious command");

	// Execute command
	std::string commandName = commands[0].first;
	std::string arguments = message.substr(msgIndex);
	CommandResult result;
	for (const std::unique_ptr<AbstractCommand> &sub : subCommands)
	{
		if (sub->getName() == commandName)
		{
			result = (*sub)(connection, sender, arguments);
			if (result.result != CommandResult::TRY_NEXT)
				break;
		}
	}

#ifdef COMMAND_DEBUG
	std::cout << Utils::format("Leaving {0}\n", name);
#endif
	return result;
}
